using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Contracts;

/// <summary>
/// The order now waits for a prepaid payment (first request or a retry). The Payments module subscribes and
/// starts the mobile-money / card payment; <see cref="Attempt"/> distinguishes retries.
/// </summary>
public sealed record OrderPaymentRequestedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    long OrderNumber,
    int Attempt,
    string PaymentMethod,
    string CustomerPhone,
    decimal AmountMzn) : IIntegrationEvent;
