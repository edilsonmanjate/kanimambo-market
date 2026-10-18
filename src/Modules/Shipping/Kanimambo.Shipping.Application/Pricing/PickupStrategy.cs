using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>
/// Free collection at a pickup point in the customer's city. The parcel still travels there: by the same-day courier
/// inside Maputo/Matola, by the regular courier otherwise — the pickup strategy reuses the other two for that.
/// </summary>
public sealed class PickupStrategy(ShippingOptions options, SameDayStrategy sameDay, CourierStrategy courier) : IShippingStrategy
{
    public const string Name = "Pickup";

    public string Method => Name;

    public string Description => "Free collection at a Kanimambo pickup point";

    public Result<ShippingLeg> Quote(ShipmentRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var point = route.PickupPointId is { Length: > 0 } id
            ? options.Pickup.Points.Find(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
            : options.Pickup.Points.Find(p => string.Equals(p.City, route.DestinationCity, StringComparison.OrdinalIgnoreCase));

        if (point is null || !string.Equals(point.City, route.DestinationCity, StringComparison.OrdinalIgnoreCase))
        {
            return ShippingErrors.NotServed(Method, $"there is no such pickup point in {route.DestinationCity}.");
        }

        if (route.WeightKg > point.MaxKg)
        {
            return ShippingErrors.NotServed(Method, $"{point.Name} accepts parcels up to {point.MaxKg:0} kg.");
        }

        if (sameDay.InArea(route.OriginCity) && sameDay.InArea(point.City))
        {
            return new ShippingLeg(Money.Zero(Currency.Mzn), options.SameDay.Courier, null, sameDay.DeliveryDate(), point.Id);
        }

        return courier.Serves(point.City)
            ? new ShippingLeg(Money.Zero(Currency.Mzn), options.Courier.Courier, null, courier.DeliveryDate(point.City), point.Id)
            : ShippingErrors.NotServed(Method, $"no courier reaches {point.City}.");
    }
}
