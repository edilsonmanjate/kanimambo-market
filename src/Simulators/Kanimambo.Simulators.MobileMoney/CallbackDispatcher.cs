using System.Security.Cryptography;
using System.Text;

namespace Kanimambo.Simulators.MobileMoney;

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
