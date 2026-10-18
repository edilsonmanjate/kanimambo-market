using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Payments.Domain;

public sealed record PaymentFailed(Guid PaymentId, Guid OrderId, int Attempt, PaymentFailureReason Reason, string Detail, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
