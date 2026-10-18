namespace Kanimambo.Simulators.Courier;

public sealed class Waybill
{
    public required string Courier { get; init; }

    public required string Reference { get; init; }

    public required string TrackingCode { get; init; }

    public required Uri WebhookUrl { get; init; }
}
