using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Infrastructure;

internal sealed class InvoicingIntegrationEventMapper : IIntegrationEventMapper
{
    public IEnumerable<IIntegrationEvent> Map(IDomainEvent domainEvent) => domainEvent switch
    {
        FiscalDocumentIssued e => [new FiscalDocumentIssuedIntegrationEvent(e.EventId, e.OccurredAt, e.DocumentId, e.Type.ToString(), e.Number, e.OrderId,
            e.OrderNumber, e.IssuerNuit, e.IssuerName, e.TotalMzn, e.CustomerPhone)],
        _ => [],
    };
}
