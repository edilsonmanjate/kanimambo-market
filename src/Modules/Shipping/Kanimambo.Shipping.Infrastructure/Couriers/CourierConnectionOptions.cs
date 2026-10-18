namespace Kanimambo.Shipping.Infrastructure.Couriers;

// The couriers below are fictional and their APIs are the ones implemented by the local simulator
// (Kanimambo.Simulators.Courier). They are deliberately different from each other to show what the adapters
// absorb. A real courier integration requires that courier's documentation and sandbox credentials.

public sealed class CourierConnectionOptions
{
    public Uri BaseAddress { get; set; } = new("https+http://courier");

    /// <summary>Shared secret for webhook signatures. Local development value only; real ones come from a vault.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
