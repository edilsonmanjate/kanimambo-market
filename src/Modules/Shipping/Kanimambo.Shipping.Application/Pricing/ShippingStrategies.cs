using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;

namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>One parcel's trip: from the seller's city to the customer (or a pickup point).</summary>
public sealed record ShipmentRoute(string OriginCity, string DestinationCity, string? District, string? PickupPointId, decimal WeightKg);

/// <summary>How a strategy would carry a parcel, and for how much.</summary>
public sealed record ShippingLeg(Money Fee, string Courier, string? Vehicle, DateOnly EstimatedDelivery, string? PickupPointId = null);

/// <summary>
/// Strategy: one way of delivering and pricing a parcel. Strategies are registered in DI and found by
/// <see cref="Method"/>; adding a delivery method means adding a class and its configuration — no switch to edit.
/// A strategy that cannot serve a route (out of area, too heavy) says so with a typed error.
/// </summary>
public interface IShippingStrategy
{
    string Method { get; }

    string Description { get; }

    Result<ShippingLeg> Quote(ShipmentRoute route);
}

internal static class ShippingErrors
{
    public static Error NotServed(string method, string reason) =>
        Error.BusinessRule("shipping.not_served", $"{method} delivery is not available: {reason}");
}

/// <summary>Same-day by motorbike or txopela inside Maputo/Matola, priced by the customer's zone plus a vehicle surcharge.</summary>
public sealed class SameDayStrategy(ShippingOptions options, TimeProvider timeProvider) : IShippingStrategy
{
    public const string Name = "SameDay";

    private SameDayOptions Settings => options.SameDay;

    public string Method => Name;

    public string Description => $"Same-day delivery in {string.Join(" / ", Settings.Cities)} by motorbike or txopela";

    public Result<ShippingLeg> Quote(ShipmentRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!InArea(route.OriginCity) || !InArea(route.DestinationCity))
        {
            return ShippingErrors.NotServed(Method, $"only between {string.Join(", ", Settings.Cities)}.");
        }

        var zone = ZoneFor(route.DestinationCity, route.District);
        if (zone is null)
        {
            return ShippingErrors.NotServed(Method, $"district '{route.District}' is outside the same-day zones of {route.DestinationCity}.");
        }

        var vehicle = Settings.Vehicles.OrderBy(v => v.MaxKg).FirstOrDefault(v => v.MaxKg >= route.WeightKg);
        if (vehicle is null)
        {
            return ShippingErrors.NotServed(Method, $"{route.WeightKg:0.###} kg is more than a txopela carries.");
        }

        return new ShippingLeg(Money.Mzn(zone.FeeMzn + vehicle.SurchargeMzn), Settings.Courier, vehicle.Name, DeliveryDate());
    }

    internal bool InArea(string city) => Settings.Cities.Contains(city, StringComparer.OrdinalIgnoreCase);

    /// <summary>Before the cut-off (Maputo time) it arrives today; after it, tomorrow.</summary>
    internal DateOnly DeliveryDate()
    {
        var now = MozambiqueTime.Now(timeProvider);
        var today = DateOnly.FromDateTime(now.DateTime);
        return TimeOnly.FromDateTime(now.DateTime) < Settings.CutoffLocalTime ? today : today.AddDays(1);
    }

    private SameDayZone? ZoneFor(string city, string? district)
    {
        if (!string.IsNullOrWhiteSpace(district))
        {
            return Settings.Zones.Find(z => z.Districts.Contains(district.Trim(), StringComparer.OrdinalIgnoreCase));
        }

        return Settings.DefaultZoneByCity.TryGetValue(city, out var name)
            ? Settings.Zones.Find(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase))
            : null;
    }
}

/// <summary>Courier (transportadora): base fee per destination plus a per-kilogram rate above the included weight.</summary>
public sealed class CourierStrategy(ShippingOptions options, TimeProvider timeProvider) : IShippingStrategy
{
    public const string Name = "Courier";

    private CourierOptions Settings => options.Courier;

    public string Method => Name;

    public string Description => "Courier delivery to every province, priced by destination and weight";

    public Result<ShippingLeg> Quote(ShipmentRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!Settings.Destinations.TryGetValue(route.DestinationCity, out var destination))
        {
            return ShippingErrors.NotServed(Method, $"the courier does not deliver to {route.DestinationCity}.");
        }

        if (route.WeightKg > Settings.MaxKg)
        {
            return ShippingErrors.NotServed(Method, $"parcels are limited to {Settings.MaxKg:0} kg.");
        }

        var extraKg = Math.Max(0m, Math.Ceiling(route.WeightKg) - Settings.IncludedKg);
        var fee = Money.Mzn(destination.BaseFeeMzn) + Money.Mzn(destination.PerKgMzn * extraKg);
        return new ShippingLeg(fee, Settings.Courier, null, DeliveryDate(destination));
    }

    internal bool Serves(string city) => Settings.Destinations.ContainsKey(city);

    internal DateOnly DeliveryDate(string city) => DeliveryDate(Settings.Destinations[city]);

    private DateOnly DeliveryDate(CourierDestination destination) => MozambiqueTime.Today(timeProvider).AddDays(destination.TransitDays);
}

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
