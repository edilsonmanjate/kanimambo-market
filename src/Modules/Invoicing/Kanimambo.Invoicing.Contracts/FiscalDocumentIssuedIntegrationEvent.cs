using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Contracts;

/// <summary>An invoice or credit note was issued (Notifications sends it to the customer in Phase 7).</summary>
public sealed record FiscalDocumentIssuedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid DocumentId,
    string DocumentType,
    string Number,
    Guid OrderId,
    long OrderNumber,
    string IssuerNuit,
    string IssuerName,
    decimal TotalMzn,
    string CustomerPhone) : IIntegrationEvent;
