using Kanimambo.Payments.Contracts;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Payments.Infrastructure;

internal sealed class PaymentsIntegrationEventMapper : IIntegrationEventMapper
{
    public IEnumerable<IIntegrationEvent> Map(IDomainEvent domainEvent) => domainEvent switch
    {
        PaymentConfirmed e => [new PaymentConfirmedIntegrationEvent(e.EventId, e.OccurredAt, e.PaymentId, e.OrderId, e.Attempt, e.Reference)],
        PaymentFailed e => [new PaymentFailedIntegrationEvent(e.EventId, e.OccurredAt, e.PaymentId, e.OrderId, e.Attempt, e.Reason.ToString(), e.Detail)],
        _ => [],
    };
}
