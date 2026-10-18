namespace Kanimambo.Notifications.Infrastructure.Channels;

// The gateway endpoints below are the local simulator's (Kanimambo.Simulators.Messaging), not any provider's real API.
// A real SMS aggregator, the WhatsApp Business API or an e-mail service needs its own adapter and credentials.

public sealed class GatewayOptions
{
    public Uri BaseAddress { get; set; } = new("https+http://messaging");

    /// <summary>Local development value only; real keys come from a vault.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The alphanumeric sender id shown on the customer's phone (max 11 characters).</summary>
    public string SmsSender { get; set; } = "KANIMAMBO";

    /// <summary>Per message; a gateway slower than this counts as down and the next channel is tried.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
