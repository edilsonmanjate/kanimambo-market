using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Promotions.Contracts;

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
