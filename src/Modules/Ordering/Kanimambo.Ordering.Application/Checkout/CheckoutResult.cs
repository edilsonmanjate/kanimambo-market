namespace Kanimambo.Ordering.Application.Checkout;

public sealed record CheckoutResult(
    Guid OrderId,
    long Number,
    string DisplayNumber,
    string Status,
    decimal Subtotal,
    string PromotionName,
    decimal Discount,
    decimal ShippingFee,
    decimal Total,
    string DeliveryMethod,
    DateOnly EstimatedDelivery,
    string NextStep,
    bool AlreadyPlaced);
