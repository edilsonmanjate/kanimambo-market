using System.Security.Cryptography;
using System.Text;

namespace Kanimambo.SharedKernel.Infrastructure.Webhooks;

/// <summary>
/// HMAC-SHA256 signature of a webhook / callback body, sent as <c>X-Signature: sha256=&lt;hex&gt;</c>.
/// Used for every inbound call from an external system (mobile-money operators, couriers).
/// </summary>
public static class WebhookSignature
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
