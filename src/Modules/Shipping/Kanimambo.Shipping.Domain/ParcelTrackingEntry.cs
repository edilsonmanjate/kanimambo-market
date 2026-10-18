namespace Kanimambo.Shipping.Domain;

public sealed record ParcelTrackingEntry(ParcelStatus Status, DateTimeOffset At, string? Detail);
