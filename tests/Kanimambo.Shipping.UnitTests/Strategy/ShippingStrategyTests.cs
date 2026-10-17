using Kanimambo.Shipping.Application.Pricing;
using static Kanimambo.Shipping.UnitTests.TestShipping;

namespace Kanimambo.Shipping.UnitTests.Strategy;

public sealed class SameDayStrategyTests
{
    private readonly SameDayStrategy _sameDay = Strategies().SameDay;

    [Theory]
    [InlineData("KaMpfumo", 150)]
    [InlineData("Nlhamankulu", 150)]
    [InlineData("KaMavota", 220)]
    [InlineData("Machava", 300)]
    public void The_fee_depends_on_the_customers_zone(string district, decimal fee) =>
        _sameDay.Quote(Route("Maputo", district == "Machava" ? "Matola" : "Maputo", 3m, district)).Value.Fee.Amount.ShouldBe(fee);

    [Theory]
    [InlineData("Maputo", 150)]
    [InlineData("Matola", 300)]
    public void Without_a_district_the_citys_default_zone_applies(string city, decimal fee) =>
        _sameDay.Quote(Route("Maputo", city, 3m)).Value.Fee.Amount.ShouldBe(fee);

    [Theory]
    [InlineData(0.5, "Motorbike", 150)]
    [InlineData(20, "Motorbike", 150)]
    [InlineData(20.1, "Txopela", 350)]
    [InlineData(150, "Txopela", 350)]
    public void The_smallest_vehicle_that_carries_the_weight_is_used(decimal kg, string vehicle, decimal fee)
    {
        var leg = _sameDay.Quote(Route("Matola", "Maputo", kg, "KaMpfumo")).Value;

        leg.Vehicle.ShouldBe(vehicle);
        leg.Fee.Amount.ShouldBe(fee);
        leg.Courier.ShouldBe("Txova");
    }

    [Theory]
    [InlineData("Beira", "Maputo", null)]
    [InlineData("Maputo", "Beira", null)]
    [InlineData("Maputo", "Maputo", "Marracuene")]
    public void Routes_outside_the_metropolitan_zones_are_not_served(string from, string to, string? district) =>
        _sameDay.Quote(Route(from, to, 3m, district)).Error.Code.ShouldBe("shipping.not_served");

    [Fact]
    public void Nothing_heavier_than_a_txopela_load() =>
        _sameDay.Quote(Route("Maputo", "Maputo", 151m)).Error.Message.ShouldContain("txopela");

    [Theory]
    [InlineData(11, 59, 0)]  // 13:59 in Maputo → today
    [InlineData(12, 0, 1)]   // 14:00 in Maputo → tomorrow
    [InlineData(21, 30, 1)]  // 23:30 in Maputo → tomorrow
    public void Orders_after_the_cut_off_arrive_the_next_day(int utcHour, int minute, int days)
    {
        var (sameDay, _, _) = Strategies(clock: Clock(new DateTimeOffset(2026, 10, 19, utcHour, minute, 0, TimeSpan.Zero)));

        sameDay.Quote(Route("Maputo", "Maputo", 1m)).Value.EstimatedDelivery.ShouldBe(Today.AddDays(days));
    }
}

public sealed class CourierStrategyTests
{
    private readonly CourierStrategy _courier = Strategies().Courier;

    [Theory]
    [InlineData("Beira", 3, 450)]       // within the 5 kg included
    [InlineData("Beira", 5, 450)]
    [InlineData("Beira", 5.2, 475)]     // every started kilogram above 5 kg: 6 − 5 = 1 × 25
    [InlineData("Beira", 12.2, 650)]    // 13 − 5 = 8 × 25
    [InlineData("Nampula", 20, 1_125)]  // 600 + 15 × 35
    public void The_fee_is_a_base_per_destination_plus_a_rate_per_kilogram(string destination, decimal kg, decimal fee) =>
        _courier.Quote(Route("Maputo", destination, kg)).Value.Fee.Amount.ShouldBe(fee);

    [Theory]
    [InlineData("Maputo", 1)]
    [InlineData("Beira", 3)]
    [InlineData("Nampula", 5)]
    public void Delivery_takes_the_destinations_transit_days(string destination, int days)
    {
        var leg = _courier.Quote(Route("Maputo", destination, 1m)).Value;

        leg.EstimatedDelivery.ShouldBe(Today.AddDays(days));
        leg.Courier.ShouldBe("Mbila");
        leg.Vehicle.ShouldBeNull();
    }

    [Fact]
    public void Destinations_without_a_rate_are_not_served() =>
        _courier.Quote(Route("Maputo", "Lichinga", 1m)).Error.Message.ShouldContain("Lichinga");

    [Fact]
    public void Parcels_above_the_limit_are_not_served() =>
        _courier.Quote(Route("Maputo", "Beira", 501m)).Error.Message.ShouldContain("500 kg");
}

public sealed class PickupStrategyTests
{
    private readonly PickupStrategy _pickup = Strategies().Pickup;

    [Fact]
    public void Collection_at_a_pickup_point_is_free_and_carried_by_the_same_day_courier_in_Maputo()
    {
        var leg = _pickup.Quote(Route("Matola", "Maputo", 10m, pickupPoint: "maputo-polana")).Value;

        leg.Fee.Amount.ShouldBe(0m);
        leg.PickupPointId.ShouldBe("maputo-polana");
        leg.Courier.ShouldBe("Txova");
        leg.EstimatedDelivery.ShouldBe(Today);
    }

    [Fact]
    public void Outside_Maputo_the_parcel_reaches_the_point_by_courier()
    {
        var leg = _pickup.Quote(Route("Maputo", "Beira", 10m, pickupPoint: "beira-macuti")).Value;

        leg.Courier.ShouldBe("Mbila");
        leg.EstimatedDelivery.ShouldBe(Today.AddDays(3));
    }

    [Fact]
    public void Without_a_point_the_first_one_in_the_city_is_used() =>
        _pickup.Quote(Route("Maputo", "Maputo", 1m)).Value.PickupPointId.ShouldBe("maputo-baixa");

    [Theory]
    [InlineData("Maputo", "beira-macuti")]   // the point is in another city
    [InlineData("Maputo", "nowhere")]
    [InlineData("Nampula", null)]           // the city has no point
    public void A_point_must_exist_in_the_customers_city(string city, string? point) =>
        _pickup.Quote(Route("Maputo", city, 1m, pickupPoint: point)).Error.Code.ShouldBe("shipping.not_served");

    [Fact]
    public void Points_have_a_weight_limit() =>
        _pickup.Quote(Route("Maputo", "Beira", 26m, pickupPoint: "beira-macuti")).Error.Message.ShouldContain("25 kg");

    [Fact]
    public void A_point_no_courier_can_reach_is_not_offered() =>
        _pickup.Quote(Route("Maputo", "Lichinga", 1m, pickupPoint: "lichinga-centro")).Error.Message.ShouldContain("no courier");
}
