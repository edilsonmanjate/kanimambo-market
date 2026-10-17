using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Application.Pricing;
using Kanimambo.Shipping.Contracts;
using static Kanimambo.Shipping.UnitTests.TestShipping;

namespace Kanimambo.Shipping.UnitTests.Strategy;

/// <summary>Strategy selection: by name from a registry, or by configured preference — never by a switch.</summary>
public sealed class ShippingQuoterTests
{
    private static readonly Guid SellerA = Guid.NewGuid();
    private static readonly Guid SellerB = Guid.NewGuid();

    private static ShippingQuoter Quoter(params IShippingStrategy[] extra)
    {
        var options = Options();
        var (sameDay, courier, pickup) = Strategies(options);
        return new ShippingQuoter(new ShippingStrategies([sameDay, courier, pickup, .. extra]), options);
    }

    private static ShippingQuoteRequest Request(string city, string? method = null, string? district = null, params ParcelRequest[] parcels) =>
        new(city, district, method, null, parcels.Length > 0 ? parcels : [new ParcelRequest(SellerA, "Maputo", 2m)]);

    [Theory]
    [InlineData("SameDay", 150)]
    [InlineData("sameday", 150)]
    [InlineData("Courier", 300)]
    [InlineData("Pickup", 0)]
    public async Task The_named_method_is_used(string method, decimal fee) =>
        (await Quoter().QuoteAsync(Request("Maputo", method), CancellationToken.None)).Value.Fee.Amount.ShouldBe(fee);

    [Fact]
    public async Task An_unknown_method_lists_the_registered_ones()
    {
        var error = (await Quoter().QuoteAsync(Request("Maputo", "Drone"), CancellationToken.None)).Error.ShouldBeOfType<ValidationError>();

        error.Errors.ShouldHaveSingleItem().Message.ShouldContain("SameDay, Courier, Pickup");
    }

    [Theory]
    [InlineData("Maputo", "SameDay")]
    [InlineData("Beira", "Courier")]     // same-day refuses Beira, the next preference serves it
    public async Task Without_a_method_the_first_preferred_method_that_serves_wins(string city, string method) =>
        (await Quoter().QuoteAsync(Request(city), CancellationToken.None)).Value.Method.ShouldBe(method);

    [Fact]
    public async Task When_nothing_serves_the_first_refusal_explains_why() =>
        (await Quoter().QuoteAsync(Request("Lichinga"), CancellationToken.None)).Error.Message.ShouldContain("SameDay delivery is not available");

    [Fact]
    public async Task Every_seller_ships_a_parcel_the_fees_add_up_and_the_slowest_parcel_sets_the_date()
    {
        var quote = (await Quoter().QuoteAsync(Request("Maputo", "SameDay", "KaMavota",
            new ParcelRequest(SellerA, "Maputo", 4m), new ParcelRequest(SellerB, "Matola", 40m)), CancellationToken.None)).Value;

        quote.Fee.ShouldBe(Money.Mzn(220m + 420m));
        quote.Parcels.Select(p => (p.SellerId, p.Vehicle)).ShouldBe([(SellerA, "Motorbike"), (SellerB, "Txopela")]);
    }

    [Fact]
    public async Task One_parcel_the_method_cannot_carry_refuses_the_whole_method() =>
        (await Quoter().QuoteAsync(Request("Maputo", "SameDay", null,
            new ParcelRequest(SellerA, "Maputo", 1m), new ParcelRequest(SellerB, "Beira", 1m)), CancellationToken.None)).IsFailure.ShouldBeTrue();

    [Fact]
    public async Task Options_list_every_method_that_serves_cheapest_first()
    {
        var options = await Quoter().OptionsAsync(Request("Maputo", "Courier"), CancellationToken.None);

        options.Select(o => (o.Method, o.Fee.Amount)).ShouldBe([("Pickup", 0m), ("SameDay", 150m), ("Courier", 300m)]);
        options[0].PickupPointId.ShouldBe("maputo-baixa");
    }

    [Fact]
    public async Task A_new_strategy_is_found_by_name_without_changing_the_quoter()
    {
        var quote = await Quoter(new ExpressBikeStrategy()).QuoteAsync(Request("Maputo", "Express"), CancellationToken.None);

        quote.Value.Method.ShouldBe("Express");
        quote.Value.Fee.ShouldBe(Money.Mzn(500m));
    }

    /// <summary>What adding a delivery method looks like: one class (plus its DI line and configuration).</summary>
    private sealed class ExpressBikeStrategy : IShippingStrategy
    {
        public string Method => "Express";

        public string Description => "Within two hours";

        public Result<ShippingLeg> Quote(ShipmentRoute route) => new ShippingLeg(Money.Mzn(500m), "Txova", "Motorbike", Today);
    }
}
