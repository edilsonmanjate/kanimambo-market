using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Catalog.Application.Stock;

/// <summary>
/// Takes the units of a paid order out of stock. Idempotent on the event id: the outbox may deliver the
/// same <see cref="OrderPaidIntegrationEvent"/> twice, the stock is reserved once.
/// </summary>
public sealed record ReserveStock(Guid IdempotencyKey, long OrderNumber, IReadOnlyList<OrderLineContract> Lines) : IIdempotentCommand;
