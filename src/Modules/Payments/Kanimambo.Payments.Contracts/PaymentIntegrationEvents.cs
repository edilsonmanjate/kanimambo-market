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

/// <summary>The payment did not happen: insufficient balance, duplicate, invalid number, timeout, unknown operator...</summary>
public sealed record PaymentFailedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PaymentId,
    Guid OrderId,
    int Attempt,
    string Reason,
    string Detail) : IIntegrationEvent;
