using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Kanimambo.Simulators.MobileMoney;

/// <summary>
/// Local stand-in for three mobile-money operators (M-Pesa, e-Mola, mKesh). Each speaks its own simulated
/// format — these are NOT the official APIs. Payments are asynchronous: the request is accepted, the
/// "customer confirms the PIN" after a delay, and the result is POSTed (HMAC-signed) to the callback URL.
/// The last four digits of the phone number choose the scenario (see <see cref="Scenario"/>).
/// </summary>
public static class MobileMoneySimulator
{
    public const string CallbackClient = "callbacks";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        var options = builder.Configuration.GetSection(SimulatorOptions.SectionName).Get<SimulatorOptions>() ?? new SimulatorOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<Ledger>();
        builder.Services.AddSingleton<CallbackDispatcher>();
        builder.Services.AddHttpClient(CallbackClient);
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.MapControllers();
        app.MapDefaultEndpoints();
        await app.RunAsync();
    }
}

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    /// <summary>How long the simulated customer takes to type the PIN.</summary>
    public int ConfirmationDelayMilliseconds { get; set; } = 3_000;

    /// <summary>For the "delayed callback" scenario: longer than the API's PIN timeout, to exercise late confirmations.</summary>
    public int DelayedCallbackMilliseconds { get; set; } = 90_000;

    /// <summary>How many times an unacknowledged callback is sent.</summary>
    public int CallbackAttempts { get; set; } = 3;

    public Dictionary<string, string> Secrets { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MPesa"] = "dev-mpesa-callback-secret",
        ["EMola"] = "dev-emola-callback-secret",
        ["MKesh"] = "dev-mkesh-callback-secret",
    };
}

public enum Scenario
{
    /// <summary>Default: the customer confirms after <see cref="SimulatorOptions.ConfirmationDelayMilliseconds"/>.</summary>
    Success,

    /// <summary>Phone ends in 0001: accepted, then fails for insufficient balance once the customer tries.</summary>
    InsufficientBalance,

    /// <summary>Phone ends in 0002: rejected immediately as a duplicate transaction.</summary>
    Duplicate,

    /// <summary>Phone ends in 0003: accepted, the customer never answers (no callback, status stays pending).</summary>
    NoAnswer,

    /// <summary>Phone ends in 0004: success, but the callback arrives after <see cref="SimulatorOptions.DelayedCallbackMilliseconds"/>.</summary>
    DelayedCallback,

    /// <summary>Phone ends in 0005: rejected immediately, the number has no wallet.</summary>
    InvalidNumber,

    /// <summary>Phone ends in 0006: the operator is down, every request gets a 503.</summary>
    Outage,
}

public enum TransactionState
{
    Pending,
    Completed,
    Failed,
}

public sealed class SimulatedTransaction
{
    public required Guid Reference { get; init; }

    public required string Operator { get; init; }

    public required string Phone { get; init; }

    public required decimal Amount { get; init; }

    public required Uri CallbackUrl { get; init; }

    public required string TransactionId { get; init; }

    public TransactionState State { get; set; }

    /// <summary>Failure reason in the simulator's own words: <c>insufficient_balance</c>.</summary>
    public string? Failure { get; set; }
}

/// <summary>In-memory record of every transaction, shared by the three operator endpoints.</summary>
public sealed class Ledger
{
    private readonly ConcurrentDictionary<Guid, SimulatedTransaction> _transactions = new();
    private readonly ConcurrentDictionary<Guid, int> _requests = new();

    public static Scenario ScenarioFor(string phone) => phone[^4..] switch
    {
        "0001" => Scenario.InsufficientBalance,
        "0002" => Scenario.Duplicate,
        "0003" => Scenario.NoAnswer,
        "0004" => Scenario.DelayedCallback,
        "0005" => Scenario.InvalidNumber,
        "0006" => Scenario.Outage,
        _ => Scenario.Success,
    };

    /// <summary>False when the reference was already used (a real duplicate).</summary>
    public bool TryOpen(SimulatedTransaction transaction) => _transactions.TryAdd(transaction.Reference, transaction);

    public SimulatedTransaction? Find(Guid reference) => _transactions.GetValueOrDefault(reference);

    /// <summary>Counts a payment request for the reference; returns how many arrived so far, this one included.</summary>
    public int CountRequest(Guid reference) => _requests.AddOrUpdate(reference, 1, (_, n) => n + 1);

    public int RequestsFor(Guid reference) => _requests.GetValueOrDefault(reference);

    /// <summary>Counts the request and tells whether to answer it with a 503 (outage scenario).</summary>
    public bool SimulateOutage(Guid reference, Scenario scenario) => CountRequest(reference) > 0 && scenario == Scenario.Outage;
}

/// <summary>Plays the customer: after a delay, settles the transaction and calls the merchant back.</summary>
public sealed partial class CallbackDispatcher(
    SimulatorOptions options,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    IHostApplicationLifetime lifetime,
    ILogger<CallbackDispatcher> logger)
{
    public void Schedule(SimulatedTransaction transaction, Scenario scenario, Func<SimulatedTransaction, string> callbackBody)
    {
        if (scenario == Scenario.NoAnswer)
        {
            return;
        }

        var delay = scenario == Scenario.DelayedCallback ? options.DelayedCallbackMilliseconds : options.ConfirmationDelayMilliseconds;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delay), timeProvider, lifetime.ApplicationStopping);
                transaction.State = scenario == Scenario.InsufficientBalance ? TransactionState.Failed : TransactionState.Completed;
                transaction.Failure = scenario == Scenario.InsufficientBalance ? "insufficient_balance" : null;

                var body = callbackBody(transaction);
                var signature = Sign(body, options.Secrets[transaction.Operator]);

                // Like real operators, retry until the merchant acknowledges (2xx), a few times.
                for (var attempt = 1; attempt <= options.CallbackAttempts; attempt++)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, transaction.CallbackUrl)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    };
                    request.Headers.Add("X-Signature", signature);

                    using var response = await httpClientFactory.CreateClient(MobileMoneySimulator.CallbackClient).SendAsync(request, lifetime.ApplicationStopping);
                    LogCallback(logger, transaction.Operator, transaction.Reference, attempt, (int)response.StatusCode);
                    if (response.IsSuccessStatusCode || (int)response.StatusCode == 401)
                    {
                        return;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(options.ConfirmationDelayMilliseconds), timeProvider, lifetime.ApplicationStopping);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException)
            {
                LogCallbackFailed(logger, exception, transaction.Reference);
            }
        });
    }

    public static string Sign(string body, string secret) =>
        $"sha256={Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)))}";

    [LoggerMessage(Level = LogLevel.Information, Message = "{Operator} callback #{Attempt} for {Reference} answered {Status}")]
    private static partial void LogCallback(ILogger logger, string @operator, Guid reference, int attempt, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Callback for {Reference} could not be delivered")]
    private static partial void LogCallbackFailed(ILogger logger, Exception exception, Guid reference);
}
