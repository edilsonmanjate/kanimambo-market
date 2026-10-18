namespace Kanimambo.Simulators.Courier;

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
