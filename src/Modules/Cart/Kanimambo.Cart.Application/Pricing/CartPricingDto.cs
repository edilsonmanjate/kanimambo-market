namespace Kanimambo.Cart.Application.Pricing;

public sealed record CartPricingDto(
    Guid CartId,
    int Version,
    string CustomerType,
    string DeliveryCity,
    string PaymentMethod,
    IReadOnlyList<PricedLineDto> Lines,
    decimal Subtotal,
    AppliedPromotionDto Promotion,
    decimal Total,
    string Currency);
