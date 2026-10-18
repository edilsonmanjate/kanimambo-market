using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Payments.Domain;

public sealed record PaymentConfirmed(Guid PaymentId, Guid OrderId, int Attempt, string Reference, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
