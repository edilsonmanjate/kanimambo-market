using Kanimambo.Shipping.Contracts;

namespace Kanimambo.Shipping.Application;

public sealed record ShippingOptionDto(string Method, string Description, decimal Fee, DateOnly EstimatedDelivery, string? PickupPointId, IReadOnlyList<ParcelQuoteDto> Parcels)
{
    public static ShippingOptionDto From(ShippingQuote q)
    {
        ArgumentNullException.ThrowIfNull(q);
        return new(q.Method, q.Description, q.Fee.Amount, q.EstimatedDelivery, q.PickupPointId,
            [.. q.Parcels.Select(p => new ParcelQuoteDto(p.SellerId, p.Fee.Amount, p.Courier, p.Vehicle, p.EstimatedDelivery))]);
    }
}
