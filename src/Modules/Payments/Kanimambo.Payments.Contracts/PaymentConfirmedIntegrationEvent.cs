using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Payments.Contracts;

/// <summary>The customer's payment went through (possibly late, after a timeout). Ordering marks the order paid.</summary>
public sealed record PaymentConfirmedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PaymentId,
    Guid OrderId,
    int Attempt,
    string Reference) : IIntegrationEvent;
