using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;

namespace Kanimambo.Shipping.Application.Pricing;

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
