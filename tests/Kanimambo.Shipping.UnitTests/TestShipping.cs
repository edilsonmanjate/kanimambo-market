using Kanimambo.Shipping.Application.Pricing;
using Microsoft.Extensions.Time.Testing;

namespace Kanimambo.Shipping.UnitTests;

internal static class TestShipping
{
    /// <summary>10:00 in Maputo (08:00 UTC): before the 14:00 same-day cut-off.</summary>
    public static readonly DateTimeOffset Morning = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Today = new(2026, 10, 19);

    public static FakeTimeProvider Clock(DateTimeOffset? at = null) => new(at ?? Morning);

    /// <summary>The same shape as appsettings.json, with round numbers.</summary>
    public static ShippingOptions Options()
    {
        var options = new ShippingOptions();
        options.DefaultMethods.AddRange(["SameDay", "Courier"]);

        var sameDay = options.SameDay;
        sameDay.Courier = "Txova";
        sameDay.Cities.AddRange(["Maputo", "Matola"]);
        sameDay.CutoffLocalTime = new TimeOnly(14, 0);
        sameDay.Zones.Add(Zone("A", 150m, "KaMpfumo", "Nlhamankulu"));
        sameDay.Zones.Add(Zone("B", 220m, "KaMavota", "KaMubukwana"));
        sameDay.Zones.Add(Zone("C", 300m, "Matola Sede", "Machava"));
        sameDay.DefaultZoneByCity["Maputo"] = "A";
        sameDay.DefaultZoneByCity["Matola"] = "C";
        sameDay.Vehicles.Add(new SameDayVehicle { Name = "Txopela", MaxKg = 150m, SurchargeMzn = 200m });
        sameDay.Vehicles.Add(new SameDayVehicle { Name = "Motorbike", MaxKg = 20m, SurchargeMzn = 0m });

        options.Courier.Courier = "Mbila";
        options.Courier.MaxKg = 500m;
        options.Courier.IncludedKg = 5m;
        options.Courier.Destinations["Maputo"] = new CourierDestination { BaseFeeMzn = 300m, PerKgMzn = 15m, TransitDays = 1 };
        options.Courier.Destinations["Beira"] = new CourierDestination { BaseFeeMzn = 450m, PerKgMzn = 25m, TransitDays = 3 };
        options.Courier.Destinations["Nampula"] = new CourierDestination { BaseFeeMzn = 600m, PerKgMzn = 35m, TransitDays = 5 };

        options.Pickup.Points.Add(new PickupPoint { Id = "maputo-baixa", Name = "Ponto Baixa", City = "Maputo", Address = "Av. 25 de Setembro", MaxKg = 30m });
        options.Pickup.Points.Add(new PickupPoint { Id = "maputo-polana", Name = "Ponto Polana", City = "Maputo", Address = "Av. Julius Nyerere", MaxKg = 30m });
        options.Pickup.Points.Add(new PickupPoint { Id = "beira-macuti", Name = "Ponto Macúti", City = "Beira", Address = "Macúti", MaxKg = 25m });
        options.Pickup.Points.Add(new PickupPoint { Id = "lichinga-centro", Name = "Ponto Lichinga", City = "Lichinga", Address = "Centro", MaxKg = 25m });
        return options;
    }

    public static (SameDayStrategy SameDay, CourierStrategy Courier, PickupStrategy Pickup) Strategies(ShippingOptions? options = null, TimeProvider? clock = null)
    {
        options ??= Options();
        clock ??= Clock();
        var sameDay = new SameDayStrategy(options, clock);
        var courier = new CourierStrategy(options, clock);
        return (sameDay, courier, new PickupStrategy(options, sameDay, courier));
    }

    public static ShipmentRoute Route(string from, string to, decimal kg, string? district = null, string? pickupPoint = null) =>
        new(from, to, district, pickupPoint, kg);

    private static SameDayZone Zone(string name, decimal fee, params string[] districts)
    {
        var zone = new SameDayZone { Name = name, FeeMzn = fee };
        zone.Districts.AddRange(districts);
        return zone;
    }
}
