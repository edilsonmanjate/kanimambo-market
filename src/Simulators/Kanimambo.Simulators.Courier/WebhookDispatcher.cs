using System.Security.Cryptography;
using System.Text;

namespace Kanimambo.Simulators.Courier;

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
