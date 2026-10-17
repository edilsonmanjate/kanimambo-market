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
                [.. e.Lines.Select(l => new OrderLineContract(l.ProductId, l.SellerId, l.Sku, l.Name, l.Quantity, l.LineTotal.Amount))])],
        OrderPaymentRequested e =>
            [new OrderPaymentRequestedIntegrationEvent(e.EventId, e.OccurredAt, e.OrderId.Value, e.Number, e.Attempt, e.PaymentMethod.ToString(), e.CustomerPhone, e.Amount.Amount)],
        OrderStatusChanged e =>
            [new OrderStatusChangedIntegrationEvent(e.EventId, e.OccurredAt, e.OrderId.Value, e.Number, e.CustomerPhone, e.From.ToString(), e.To.ToString(), e.Reason)],
        _ => [],
    };
}
