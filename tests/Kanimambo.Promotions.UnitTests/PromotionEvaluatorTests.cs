using Kanimambo.Promotions.Application;
using Kanimambo.Promotions.Contracts;
using Kanimambo.Promotions.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Time.Testing;

namespace Kanimambo.Promotions.UnitTests;

public sealed class PromotionEvaluatorTests
{
    // Saturday 17 October 2026, 10:00 in Maputo.
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPromotions _promotions = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_applicable_rule_returns_the_no_promotion_null_object()
    {
        _promotions.Add(Promotion("Só em Beira", "customer.city = \"Beira\"", DiscountKind.Percentage, 10m));

        var applied = await Evaluator().BestPromotionAsync(Context(1_000m, city: "Maputo"), Ct);

        applied.ShouldBeSameAs(NoPromotion.Instance);
        applied.Discount.ShouldBe(Money.Zero(Currency.Mzn));
        applied.Name.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_largest_discount_wins_when_several_rules_apply()
    {
        _promotions.Add(Promotion("Cabazes 150", "\"cabazes\" IN cart.categories", DiscountKind.FixedAmount, 150m));
        _promotions.Add(Promotion("M-Pesa 5%", "payment.method = \"MPesa\"", DiscountKind.Percentage, 5m));

        var small = await Evaluator().BestPromotionAsync(Context(2_000m, payment: PaymentMethod.MPesa), Ct);
        var large = await Evaluator().BestPromotionAsync(Context(5_000m, payment: PaymentMethod.MPesa), Ct);

        small.Name.ShouldBe("Cabazes 150");
        small.Discount.ShouldBe(Money.Mzn(150m));
        large.Name.ShouldBe("M-Pesa 5%");
        large.Discount.ShouldBe(Money.Mzn(250m));
    }

    [Fact]
    public async Task Inactive_and_out_of_window_promotions_are_ignored()
    {
        var inactive = Promotion("Inactiva", "TRUE", DiscountKind.Percentage, 50m);
        inactive.Deactivate();
        _promotions.Add(inactive);
        _promotions.Add(Promotion("Futura", "TRUE", DiscountKind.Percentage, 40m, validFrom: new DateOnly(2026, 12, 1)));
        _promotions.Add(Promotion("Expirada", "TRUE", DiscountKind.Percentage, 30m, validUntil: new DateOnly(2026, 10, 16)));
        _promotions.Add(Promotion("Hoje", "TRUE", DiscountKind.Percentage, 1m, validFrom: new DateOnly(2026, 10, 17), validUntil: new DateOnly(2026, 10, 17)));

        (await Evaluator().BestPromotionAsync(Context(1_000m), Ct)).Name.ShouldBe("Hoje");
    }

    [Fact]
    public async Task Weekday_comes_from_maputo_time()
    {
        _promotions.Add(Promotion("Sábado", "today.weekday = \"Saturday\"", DiscountKind.FixedAmount, 50m));

        (await Evaluator().BestPromotionAsync(Context(1_000m), Ct)).Name.ShouldBe("Sábado");

        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 17, 22, 30, 0, TimeSpan.Zero)); // already Sunday 00:30 in Maputo
        (await Evaluator().BestPromotionAsync(Context(1_000m), Ct)).ShouldBeSameAs(NoPromotion.Instance);
    }

    [Theory]
    [InlineData(DiscountKind.Percentage, 5, 2_570, 128.50)]
    [InlineData(DiscountKind.FixedAmount, 150, 2_570, 150)]
    [InlineData(DiscountKind.FixedAmount, 500, 300, 300)]
    [InlineData(DiscountKind.Percentage, 100, 999.99, 999.99)]
    public void Discounts_never_exceed_the_subtotal(DiscountKind kind, decimal value, decimal subtotal, decimal expected)
    {
        PromotionDiscount.Create(kind, value).Value.AmountFor(Money.Mzn(subtotal)).ShouldBe(Money.Mzn(expected));
    }

    [Theory]
    [InlineData(DiscountKind.Percentage, 0)]
    [InlineData(DiscountKind.Percentage, 101)]
    [InlineData(DiscountKind.FixedAmount, -1)]
    public void Invalid_discounts_are_rejected(DiscountKind kind, decimal value)
    {
        PromotionDiscount.Create(kind, value).Error.ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public void A_promotion_with_an_invalid_rule_cannot_be_created()
    {
        var created = Domain.Promotion.Create("Errada", "cart.total >= 2500 AN TRUE", PromotionDiscount.Create(DiscountKind.Percentage, 5m).Value, null, null, "ana", _clock.GetUtcNow());

        created.Error.ShouldBeOfType<ValidationError>().Errors.ShouldHaveSingleItem().Message.ShouldStartWith("Position 20:");
    }

    [Fact]
    public void The_rule_is_stored_as_written_and_re_parsed_on_demand()
    {
        var promotion = Promotion("Formatação livre", "cart.total>=2500   and   customer.city=\"Maputo\"", DiscountKind.Percentage, 5m);

        promotion.RuleText.ShouldBe("cart.total>=2500   and   customer.city=\"Maputo\"");
        promotion.Rule.ToString().ShouldBe("cart.total >= 2500 AND customer.city = \"Maputo\"");
    }

    private PromotionEvaluator Evaluator() => new(_promotions, _clock);

    private static PromotionContext Context(decimal total, string city = "Maputo", PaymentMethod payment = PaymentMethod.None) =>
        new(Money.Mzn(total), 3, ["mercearia", "cabazes"], city, "guest", payment);

    private Promotion Promotion(string name, string rule, DiscountKind kind, decimal value, DateOnly? validFrom = null, DateOnly? validUntil = null)
    {
        _clock.Advance(TimeSpan.FromSeconds(1));
        return Domain.Promotion.Create(name, rule, PromotionDiscount.Create(kind, value).Value, validFrom, validUntil, "ana", _clock.GetUtcNow()).Value;
    }

    private sealed class InMemoryPromotions : IPromotionRepository
    {
        private readonly List<Promotion> _items = [];

        public void Add(Promotion promotion) => _items.Add(promotion);

        public Task<Promotion?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_items.Find(p => p.Id == id));

        public Task<IReadOnlyList<Promotion>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Promotion>>(_items.Where(p => !activeOnly || p.IsActive).ToList());
    }
}
