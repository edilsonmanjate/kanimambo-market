using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application.Payments;

public sealed record ApplyPaymentFailure(Guid IdempotencyKey, Guid OrderId, int Attempt, string Reason) : IIdempotentCommand;
