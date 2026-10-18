using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Invoicing.Domain;

public sealed record FiscalDocumentIssued(
    Guid DocumentId, DocumentType Type, string Number, Guid OrderId, long OrderNumber, string IssuerNuit, string IssuerName, decimal TotalMzn,
    string CustomerPhone, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
