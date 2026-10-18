namespace Kanimambo.Promotions.Contracts;

public interface IPromotionEvaluator
{
    /// <summary>The best applicable promotion, or <see cref="NoPromotion.Instance"/>. Never null.</summary>
    Task<IAppliedPromotion> BestPromotionAsync(PromotionContext context, CancellationToken cancellationToken);
}
