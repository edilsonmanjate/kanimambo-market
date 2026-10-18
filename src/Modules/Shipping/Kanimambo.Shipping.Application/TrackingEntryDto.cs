namespace Kanimambo.Shipping.Application;

public sealed record TrackingEntryDto(string Status, DateTimeOffset At, string? Detail);
