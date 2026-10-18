using Kanimambo.Shipping.Application.Pricing;
using static Kanimambo.Shipping.UnitTests.TestShipping;

namespace Kanimambo.Shipping.UnitTests.Strategy;

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
