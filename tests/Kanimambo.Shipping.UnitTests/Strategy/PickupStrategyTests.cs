using Kanimambo.Shipping.Application.Pricing;
using static Kanimambo.Shipping.UnitTests.TestShipping;

namespace Kanimambo.Shipping.UnitTests.Strategy;

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
