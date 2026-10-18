using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application.Payments;

public sealed record ApplyPaymentConfirmation(Guid IdempotencyKey, Guid OrderId, int Attempt, string Reference) : IIdempotentCommand;
