namespace Kanimambo.Shipping.Application;

public sealed record ParcelDto(
    Guid Id,
    string Reference,
    Guid SellerId,
    string OriginCity,
    decimal WeightKg,
    string Courier,
    string? Vehicle,
    decimal CashToCollect,
    string Status,
    string? TrackingCode,
    string? FailureReason,
    IReadOnlyList<TrackingEntryDto> Tracking);
