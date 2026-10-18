using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

public sealed record GetOrder(Guid OrderId) : IQuery<OrderDto>;
