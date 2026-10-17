using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Kanimambo.Simulators.Courier;

/// <summary>
/// Local stand-in for two <em>fictional</em> couriers: "Txova Express" (same-day riders in Maputo/Matola) and
/// "Mbila Cargo" (inter-provincial cargo). Each speaks its own simulated format. A booking is accepted, then the
/// courier reports progress (picked up, in transit, delivered) through HMAC-signed webhooks, retried until the
/// merchant acknowledges them. The last four digits of the recipient's phone choose the scenario (see <see cref="Scenario"/>).
/// </summary>
public static class CourierSimulator
{
    public const string WebhookClient = "webhooks";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        var options = builder.Configuration.GetSection(SimulatorOptions.SectionName).Get<SimulatorOptions>() ?? new SimulatorOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<Waybills>();
        builder.Services.AddSingleton<WebhookDispatcher>();
        builder.Services.AddHttpClient(WebhookClient);
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

    /// <summary>Time until the rider / truck collects the parcel at the seller.</summary>
    public int PickupDelayMilliseconds { get; set; } = 3_000;

    /// <summary>Inter-provincial only: time from collection to "in transit".</summary>
    public int TransitDelayMilliseconds { get; set; } = 3_000;

    /// <summary>Time from the previous step to delivery (or the failed attempt).</summary>
    public int DeliveryDelayMilliseconds { get; set; } = 5_000;

    /// <summary>How many times an unacknowledged webhook is sent.</summary>
    public int WebhookAttempts { get; set; } = 3;

    public Dictionary<string, string> Secrets { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Txova"] = "dev-txova-webhook-secret",
        ["Mbila"] = "dev-mbila-webhook-secret",
    };
}

public enum Scenario
{
    /// <summary>Default: collected, (in transit,) delivered.</summary>
    Success,

    /// <summary>Recipient phone ends in 0101: nobody at home, the delivery attempt fails.</summary>
    RecipientAbsent,

    /// <summary>Recipient phone ends in 0102: the courier refuses the booking (does not serve the destination).</summary>
    Refused,

    /// <summary>Recipient phone ends in 0103: the first booking request for a parcel gets a 503; retries succeed.</summary>
    Outage,
}

public sealed class Waybill
{
    public required string Courier { get; init; }

    public required string Reference { get; init; }

    public required string TrackingCode { get; init; }

    public required Uri WebhookUrl { get; init; }
}

/// <summary>Every booking, by our customer's reference: booking the same reference again returns the same waybill.</summary>
public sealed class Waybills
{
    private readonly ConcurrentDictionary<string, Waybill> _byReference = new();
    private readonly ConcurrentDictionary<string, int> _requests = new();

    public static Scenario ScenarioFor(string phone) => phone.Length < 4 ? Scenario.Success : phone[^4..] switch
    {
        "0101" => Scenario.RecipientAbsent,
        "0102" => Scenario.Refused,
        "0103" => Scenario.Outage,
        _ => Scenario.Success,
    };

    /// <summary>Counts the booking request; true when it should be answered with a 503 (first request of the outage scenario).</summary>
    public bool SimulateOutage(string reference, Scenario scenario) =>
        _requests.AddOrUpdate(reference, 1, (_, n) => n + 1) == 1 && scenario == Scenario.Outage;

    public int RequestsFor(string reference) => _requests.GetValueOrDefault(reference);

    /// <summary>The waybill for the reference, and whether this request created it.</summary>
    public (Waybill Waybill, bool Created) Open(string reference, Func<Waybill> create)
    {
        var created = false;
        var waybill = _byReference.GetOrAdd(reference, _ =>
        {
            created = true;
            return create();
        });
        return (waybill, created);
    }
}

/// <summary>Plays the courier's operations: after each delay, the next tracking event is sent to the merchant.</summary>
public sealed partial class WebhookDispatcher(
    SimulatorOptions options,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    IHostApplicationLifetime lifetime,
    ILogger<WebhookDispatcher> logger)
{
    /// <summary>Sends each (delay, body) step in order, signed with the courier's secret.</summary>
    public void Schedule(Waybill waybill, IReadOnlyList<(int DelayMilliseconds, string Body)> steps) =>
        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var (delay, body) in steps)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delay), timeProvider, lifetime.ApplicationStopping);
                    await SendAsync(waybill, body);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException)
            {
                LogWebhookFailed(logger, exception, waybill.Reference);
            }
        });

    public static string Sign(string body, string secret) =>
        $"sha256={Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)))}";

    private async Task SendAsync(Waybill waybill, string body)
    {
        var signature = Sign(body, options.Secrets[waybill.Courier]);
        for (var attempt = 1; attempt <= options.WebhookAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, waybill.WebhookUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Signature", signature);

            using var response = await httpClientFactory.CreateClient(CourierSimulator.WebhookClient).SendAsync(request, lifetime.ApplicationStopping);
            LogWebhook(logger, waybill.Courier, waybill.Reference, attempt, (int)response.StatusCode);
            if (response.IsSuccessStatusCode || (int)response.StatusCode == 401)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, options.PickupDelayMilliseconds)), timeProvider, lifetime.ApplicationStopping);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Courier} webhook #{Attempt} for {Reference} answered {Status}")]
    private static partial void LogWebhook(ILogger logger, string courier, string reference, int attempt, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhooks for {Reference} stopped")]
    private static partial void LogWebhookFailed(ILogger logger, Exception exception, string reference);
}
