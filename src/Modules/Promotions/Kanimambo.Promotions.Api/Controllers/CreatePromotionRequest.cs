namespace Kanimambo.Promotions.Api.Controllers;

public sealed record CreatePromotionRequest(
    string Name,
    string Rule,
    string DiscountKind,
    decimal DiscountValue,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);
