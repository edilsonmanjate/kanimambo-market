using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;

namespace Kanimambo.Shipping.Application.Pricing;

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
