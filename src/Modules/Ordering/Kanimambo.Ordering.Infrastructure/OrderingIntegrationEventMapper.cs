using Kanimambo.Ordering.Contracts;
using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Infrastructure;

/// <summary>Decides what Ordering publishes: internal domain events become public integration events.</summary>
internal sealed class OrderingIntegrationEventMapper : IIntegrationEventMapper
{
    public IEnumerable<IIntegrationEvent> Map(IDomainEvent domainEvent) => domainEvent switch
    {
        OrderPlaced e =>
            [new OrderPlacedIntegrationEvent(e.EventId, e.OccurredAt, e.OrderId.Value, e.Number, e.CheckoutSource, e.CheckoutSourceId, e.CustomerId, e.Total.Amount)],
        OrderPaid e =>
            [new OrderPaidIntegrationEvent(e.EventId, e.OccurredAt, e.OrderId.Value, e.Number, e.Customer.Id, e.Customer.Phone, e.PaymentMethod.ToString(), e.Total.Amount,
                [.. e.Lines.Select(l => new OrderLineContract(l.ProductId, l.SellerId, l.Sku, l.Name, l.Quantity, l.LineTotal.Amount))],
                e.Customer.Type, e.Customer.Nuit, e.Totals.Subtotal.Amount, e.Totals.Discount.Amount, e.Totals.ShippingFee.Amount, e.DeliveryMethod)],
        OrderPaymentRequested e =>
            [new OrderPaymentRequestedIntegrationEvent(e.EventId, e.OccurredAt, e.OrderId.Value, e.Number, e.Attempt, e.PaymentMethod.ToString(), e.CustomerPhone, e.Amount.Amount)],
        OrderDispatched e =>
            [new OrderDispatchedIntegrationEvent(e.EventId, e.OccurredAt, e.OrderId.Value, e.Number, e.Delivery.Method, e.DeliveryCity, e.Delivery.District,
                e.Delivery.PickupPointId, e.CustomerPhone, e.CashToCollect.Amount,
                [.. e.Parcels.Select(p => new DispatchedParcelContract(p.SellerId, p.OriginCity, p.WeightKg))])],
        OrderStatusChanged e =>
            [new OrderStatusChangedIntegrationEvent(e.EventId, e.OccurredAt, e.OrderId.Value, e.Number, e.CustomerPhone, e.From.ToString(), e.To.ToString(), e.Reason)],
        _ => [],
    };
}
