using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application.Shipping;

public sealed record ApplyDelivery(Guid IdempotencyKey, Guid OrderId) : IIdempotentCommand;
