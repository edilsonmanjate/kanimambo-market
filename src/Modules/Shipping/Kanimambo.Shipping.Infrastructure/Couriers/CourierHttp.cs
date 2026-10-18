using System.Text.Json;
using Kanimambo.SharedKernel.Infrastructure.Webhooks;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Application.Couriers;

namespace Kanimambo.Shipping.Infrastructure.Couriers;

internal static class CourierHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Sends the request; outages, 5xx and timeouts become a transient <see cref="CourierError"/>.</summary>
    public static async Task<Result<HttpResponseMessage>> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            var response = await send();
            if ((int)response.StatusCode >= 500)
            {
                response.Dispose();
                return Unavailable($"The courier answered {(int)response.StatusCode}.");
            }

            return response;
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("The courier did not answer in time.");
        }
    }

    public static Result<T> ParseSigned<T>(string body, string? signature, string secret)
    {
        if (!WebhookSignature.IsValid(body, signature, secret))
        {
            return Error.Unauthorized("shipping.invalid_signature", "The webhook signature is not valid.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(body, Json);
            return parsed is null ? Error.Validation(new FieldError("body", "Empty webhook.")) : parsed;
        }
        catch (JsonException exception)
        {
            return Error.Validation(new FieldError("body", $"Unreadable webhook: {exception.Message}"));
        }
    }

    public static CourierError Unavailable(string message) => new(CourierFailureReason.Unavailable, null, message);
}
