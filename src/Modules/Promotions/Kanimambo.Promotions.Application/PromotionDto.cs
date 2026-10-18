using Kanimambo.Promotions.Domain;

namespace Kanimambo.Promotions.Application;

public sealed record PromotionDto(
    Guid Id,
    string Name,
    string Rule,
    string CanonicalRule,
    string DiscountKind,
    decimal DiscountValue,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsActive,
    string CreatedBy,
    DateTimeOffset CreatedAt)
{
    public static PromotionDto From(Promotion promotion)
    {
        ArgumentNullException.ThrowIfNull(promotion);
        return new PromotionDto(promotion.Id, promotion.Name, promotion.RuleText, promotion.Rule.ToString(),
            promotion.Discount.Kind.ToString(), promotion.Discount.Value, promotion.ValidFrom, promotion.ValidUntil,
            promotion.IsActive, promotion.CreatedBy, promotion.CreatedAt);
    }
}
