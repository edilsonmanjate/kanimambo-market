using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Promotions.Application;

/// <summary>Discount kind is <c>Percentage</c> or <c>FixedAmount</c> (MZN).</summary>
public sealed record CreatePromotion(
    string Name,
    string Rule,
    string DiscountKind,
    decimal DiscountValue,
    DateOnly? ValidFrom = null,
    DateOnly? ValidUntil = null) : ICommand<PromotionDto>;
