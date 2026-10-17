using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Infrastructure.Gateways.Adapters;

/// <summary>HMAC-SHA256 signature of a callback body: <c>X-Signature: sha256=&lt;hex&gt;</c>.</summary>
public static class CallbackSignature
{
    public const string Header = "X-Signature";

    public static string Sign(string body, string secret)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(secret);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }

    public static bool IsValid(string body, string? signature, string secret) =>
        signature is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(body, secret)), Encoding.ASCII.GetBytes(signature));
}

/// <summary>HTTP plumbing shared by the operator adapters; the translation of each format stays in each adapter.</summary>
internal static class GatewayHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<Result<TResponse>> SendAsync<TResponse>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            if ((int)response.StatusCode >= 500)
            {
                return Unavailable($"The operator answered {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken);
            return body is null ? Unavailable("The operator returned an empty response.") : body;
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new GatewayError(PaymentFailureReason.Timeout, null, "The operator did not answer in time.");
        }
        catch (JsonException exception)
        {
            return Unavailable($"Unreadable operator response: {exception.Message}");
        }
    }

    public static Result<T> ParseSigned<T>(string body, string? signature, string secret)
    {
        if (!CallbackSignature.IsValid(body, signature, secret))
        {
            return Error.Unauthorized("payments.invalid_signature", "The callback signature is not valid.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(body, Json);
            return parsed is null ? Error.Validation(new FieldError("body", "Empty callback.")) : parsed;
        }
        catch (JsonException exception)
        {
            return Error.Validation(new FieldError("body", $"Unreadable callback: {exception.Message}"));
        }
    }

    private static GatewayError Unavailable(string message) => new(PaymentFailureReason.GatewayUnavailable, null, message);
}

public sealed class OperatorOptions
{
    public IReadOnlyList<string> Prefixes { get; set; } = [];

    public Uri BaseAddress { get; set; } = new("https+http://mobile-money");

    /// <summary>Shared secret for callback signatures. Local development value only; real ones come from a vault.</summary>
    public string CallbackSecret { get; set; } = string.Empty;
}
