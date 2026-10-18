using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

public sealed record ListMyOrders : IQuery<IReadOnlyList<OrderDto>>;
