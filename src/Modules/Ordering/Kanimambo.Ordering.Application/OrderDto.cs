using Kanimambo.Ordering.Domain;

namespace Kanimambo.Ordering.Application;

public sealed record OrderDto(
    Guid Id,
    long Number,
    string DisplayNumber,
    string Status,
    IReadOnlyList<string> AllowedActions,
    string CustomerType,
    string DeliveryCity,
    string PaymentMethod,
    string? PaymentReference,
    IReadOnlyList<OrderLineDto> Lines,
    decimal Subtotal,
    string PromotionName,
    decimal Discount,
    decimal ShippingFee,
    decimal Total,
    string DeliveryMethod,
    string? DeliveryDistrict,
    string? PickupPointId,
    DateOnly EstimatedDelivery,
    DateTimeOffset PlacedAt,
    IReadOnlyList<OrderStatusChangeDto> History)
{
    public static OrderDto From(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var totals = order.Totals;
        return new OrderDto(order.Id.Value, order.Number, order.DisplayNumber, order.Status.ToString(),
            [.. order.AllowedActions.Select(a => a.ToString())], order.Customer.Type, order.DeliveryCity, order.PaymentMethod.ToString(),
            order.PaymentReference, [.. order.Lines.Select(Line)], totals.Subtotal.Amount, totals.PromotionName, totals.Discount.Amount,
            totals.ShippingFee.Amount, totals.Total.Amount, order.Delivery.Method, order.Delivery.District, order.Delivery.PickupPointId,
            order.Delivery.EstimatedDelivery, order.PlacedAt,
            [.. order.History.Select(h => new OrderStatusChangeDto(h.From.ToString(), h.To.ToString(), h.At, h.Reason))]);
    }

    internal static OrderLineDto Line(OrderLine l) => new(l.ProductId, l.SellerId, l.Sku, l.Name, l.Quantity, l.UnitPrice.Amount, l.LineTotal.Amount);
}
