using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Promotions.Contracts;

/// <summary>The promotion that applies to a cart, with the discount already computed for its subtotal.</summary>
public interface IAppliedPromotion
{
    Guid PromotionId { get; }

    string Name { get; }

    Money Discount { get; }
}
