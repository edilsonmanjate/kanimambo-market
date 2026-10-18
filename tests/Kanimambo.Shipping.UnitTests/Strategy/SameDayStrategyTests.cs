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
