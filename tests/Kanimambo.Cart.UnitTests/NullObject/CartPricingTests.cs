using System.Reflection;
using Kanimambo.Cart.Application.Pricing;
using Kanimambo.Cart.Domain;
using Kanimambo.Catalog.Contracts;
using Kanimambo.Promotions.Contracts;
using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Cart.UnitTests.NullObject;

public sealed class CartPricingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly CatalogItemSnapshot Arroz = Item("MX-ARROZ-25", Money.Mzn(1_850m), stock: 40);
    private static readonly CatalogItemSnapshot Lanterna = Item("ST-LANTERNA", Money.Of(18.50m, Currency.Usd), stock: 3);

    [Fact]
    public void A_guest_paying_in_mzn_without_promotion_goes_through_the_same_path_as_everyone()
    {
        var cart = Cart((Arroz, 2));

        var priced = Price(cart, NoPromotion.Instance);

        priced.Customer.ShouldBeSameAs(GuestCustomer.Instance);
        priced.Lines.ShouldHaveSingleItem().Rate.ShouldBeSameAs(NoExchangeRate.For(Currency.Mzn));
        priced.Subtotal.ShouldBe(Money.Mzn(3_700m));
        priced.Promotion.ShouldBeSameAs(NoPromotion.Instance);
        priced.Total.ShouldBe(Money.Mzn(3_700m));
    }

    [Fact]
    public void Foreign_prices_are_converted_with_their_rate_and_the_promotion_is_subtracted()
    {
        var cart = Cart((Arroz, 1), (Lanterna, 2));

        var priced = Price(cart, new TestPromotion("Cabazes", Money.Mzn(150m)));

        priced.Lines.Single(l => l.Sku == "ST-LANTERNA").LineTotalMzn.ShouldBe(Money.Mzn(18.50m * 2 * 63.90m));
        priced.Subtotal.ShouldBe(Money.Mzn(1_850m + 2_364.30m));
        priced.Total.ShouldBe(Money.Mzn(1_850m + 2_364.30m - 150m));
    }

    [Fact]
    public void Unavailable_products_are_shown_but_not_charged_and_short_stock_is_flagged()
    {
        var cart = Cart((Arroz, 1), (Lanterna, 5));
        var gone = Guid.NewGuid();
        cart.SetQuantity(gone, "GONE", "Produto retirado", 1, Now);

        var priced = Price(cart, NoPromotion.Instance);

        priced.Lines.Select(l => (l.Sku, l.Availability)).ShouldBe([
            ("MX-ARROZ-25", LineAvailability.Available),
            ("ST-LANTERNA", LineAvailability.InsufficientStock),
            ("GONE", LineAvailability.Unavailable),
        ]);
        priced.Lines.Single(l => l.Sku == "GONE").Rate.ShouldBeSameAs(NoExchangeRate.For(Currency.Mzn));
        priced.Subtotal.ShouldBe(Money.Mzn(1_850m + (18.50m * 5 * 63.90m)));
    }

    [Fact]
    public void No_exchange_rate_is_the_identity_and_refuses_other_currencies()
    {
        var mzn = NoExchangeRate.For(Currency.Mzn);

        mzn.Convert(Money.Mzn(42m)).ShouldBe(Money.Mzn(42m));
        mzn.Rate.ShouldBe(1m);
        NoExchangeRate.For(Currency.Mzn).ShouldBeSameAs(mzn);
        Should.Throw<InvalidOperationException>(() => mzn.Convert(Money.Of(1m, Currency.Usd)));
    }

    [Fact]
    public void The_checkout_pricing_contract_has_no_nullable_customer_promotion_or_rate()
    {
        // The null objects make "nothing" a real object; the types prove callers never receive null.
        var nullability = new NullabilityInfoContext();
        var types = new[] { typeof(PricedCart), typeof(PricedLine) };

        var nullable = types
            .SelectMany(t => t.GetProperties())
            .Where(p => p.PropertyType == typeof(Customer) || p.PropertyType == typeof(IAppliedPromotion) || p.PropertyType == typeof(IExchangeRate))
            .Where(p => nullability.Create(p).ReadState != NullabilityState.NotNull)
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        nullable.ShouldBeEmpty();
        typeof(IPromotionEvaluator).GetMethod(nameof(IPromotionEvaluator.BestPromotionAsync))!.ReturnType
            .ShouldBe(typeof(Task<IAppliedPromotion>));
    }

    private static PricedCart Price(ShoppingCart cart, IAppliedPromotion promotion)
    {
        var items = new[] { Arroz, Lanterna }.ToDictionary(i => i.ProductId);
        var rates = new Dictionary<Currency, IExchangeRate>
        {
            [Currency.Mzn] = NoExchangeRate.For(Currency.Mzn),
            [Currency.Usd] = new FixedRate(Currency.Usd, 63.90m),
        };

        return CartPricing.Complete(cart.Customer, CartPricing.PriceLines(cart, items, rates), promotion);
    }

    private static ShoppingCart Cart(params (CatalogItemSnapshot Item, int Quantity)[] lines)
    {
        var cart = ShoppingCart.Open(CartId.New(), GuestCustomer.Instance, "Maputo", Now).Value;
        foreach (var (item, quantity) in lines)
        {
            cart.SetQuantity(item.ProductId, item.Sku, item.Name, quantity, Now);
        }

        return cart;
    }

    private static CatalogItemSnapshot Item(string sku, Money price, int stock) =>
        new(Guid.NewGuid(), Guid.NewGuid(), sku, sku, "mercearia", price, stock, 1m);

    private sealed record TestPromotion(string Name, Money Discount) : IAppliedPromotion
    {
        public Guid PromotionId { get; } = Guid.NewGuid();
    }

    private sealed record FixedRate(Currency From, decimal Rate) : IExchangeRate
    {
        public Currency To => Currency.Mzn;

        public Money Convert(Money amount) => Money.Mzn(amount.Amount * Rate);
    }
}
