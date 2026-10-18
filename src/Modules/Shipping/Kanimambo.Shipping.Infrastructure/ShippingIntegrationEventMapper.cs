using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.Shipping.Contracts;
using Kanimambo.Shipping.Domain;

namespace Kanimambo.Shipping.Infrastructure;

internal sealed class ShippingIntegrationEventMapper : IIntegrationEventMapper
{
    public IEnumerable<IIntegrationEvent> Map(IDomainEvent domainEvent) => domainEvent switch
    {
        ParcelStatusChanged e => [new ParcelStatusChangedIntegrationEvent(e.EventId, e.OccurredAt, e.ConsignmentId, e.OrderId, e.OrderNumber, e.ParcelId,
            e.Status.ToString(), e.Courier, e.TrackingCode, e.RecipientPhone, e.Detail)],
        ConsignmentDelivered e => [new OrderDeliveredIntegrationEvent(e.EventId, e.OccurredAt, e.ConsignmentId, e.OrderId, e.OrderNumber)],
        ConsignmentFailed e => [new DeliveryFailedIntegrationEvent(e.EventId, e.OccurredAt, e.ConsignmentId, e.OrderId, e.OrderNumber, e.ParcelId, e.Reason)],
        _ => [],
    };
}
