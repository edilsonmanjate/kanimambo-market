using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Promotions.Contracts;

/// <summary>What the promotion rules may look at for one cart.</summary>
public sealed record PromotionContext(
    Money CartTotal,
    int ItemCount,
    IReadOnlyCollection<string> Categories,
    string CustomerCity,
    string CustomerType,
    PaymentMethod PaymentMethod);

/// <summary>The promotion that applies to a cart, with the discount already computed for its subtotal.</summary>
public interface IAppliedPromotion
{
    Guid PromotionId { get; }

    string Name { get; }

    Money Discount { get; }
}

/// <summary>
/// Null Object: "no promotion applies". Its discount is zero, so pricing code always subtracts
/// <see cref="IAppliedPromotion.Discount"/> without asking whether a promotion exists.
/// </summary>
public sealed class NoPromotion : IAppliedPromotion
{
    public static NoPromotion Instance { get; } = new();

    private NoPromotion()
    {
    }

    public Guid PromotionId => Guid.Empty;

    public string Name => string.Empty;

    public Money Discount => Money.Zero(Currency.Mzn);
}

public interface IPromotionEvaluator
{
    /// <summary>The best applicable promotion, or <see cref="NoPromotion.Instance"/>. Never null.</summary>
    Task<IAppliedPromotion> BestPromotionAsync(PromotionContext context, CancellationToken cancellationToken);
}
