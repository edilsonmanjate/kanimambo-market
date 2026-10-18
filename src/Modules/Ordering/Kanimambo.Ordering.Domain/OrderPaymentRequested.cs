using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Domain;

/// <summary>The order waits for a prepaid payment; <paramref name="Attempt"/> starts at 1 and grows with each retry.</summary>
public sealed record OrderPaymentRequested(OrderId OrderId, long Number, int Attempt, PaymentMethod PaymentMethod, string CustomerPhone, Money Amount, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt);
