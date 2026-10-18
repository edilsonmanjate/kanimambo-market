using Kanimambo.Shipping.Domain;

namespace Kanimambo.Shipping.Application;

public sealed record ConsignmentDto(
    Guid Id,
    Guid OrderId,
    long OrderNumber,
    string Method,
    string DestinationCity,
    string? District,
    string? PickupPointId,
    string Status,
    IReadOnlyList<ParcelDto> Parcels)
{
    public static ConsignmentDto From(Consignment c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new(c.Id, c.OrderId, c.OrderNumber, c.Method, c.DestinationCity, c.District, c.PickupPointId, c.Status.ToString(),
            [.. c.Parcels.Select(p => new ParcelDto(p.Id, p.Reference, p.SellerId, p.OriginCity, p.WeightKg, p.Courier, p.Vehicle, p.CashToCollect.Amount,
                p.Status.ToString(), p.TrackingCode, p.FailureReason, [.. p.Tracking.Select(t => new TrackingEntryDto(t.Status.ToString(), t.At, t.Detail))]))]);
    }
}
