using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Catalog.Application.Stock;

/// <summary>
/// Takes the units of a paid order out of stock. Idempotent on the event id: the outbox may deliver the
/// same <see cref="OrderPaidIntegrationEvent"/> twice, the stock is reserved once.
/// </summary>
public sealed record ReserveStock(Guid IdempotencyKey, long OrderNumber, IReadOnlyList<OrderLineContract> Lines) : IIdempotentCommand;

/// <summary>Observer: Catalog subscribes to Ordering's event without Ordering knowing about it.</summary>
public sealed class ReserveStockWhenOrderIsPaid(ISender sender) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async ValueTask Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var result = await sender.Send(new ReserveStock(notification.EventId, notification.OrderNumber, notification.Lines), cancellationToken);
        if (result.IsFailure)
        {
            // Surfaces to the outbox, which records the failure and retries this subscriber only.
            throw new InvalidOperationException($"Stock reservation for order {notification.OrderNumber} failed: {result.Error.Message}");
        }
    }
}

public sealed partial class ReserveStockHandler(ICatalogItemRepository products, ILogger<ReserveStockHandler> logger)
    : IRequestHandler<ReserveStock, Result>
{
    public async ValueTask<Result> Handle(ReserveStock request, CancellationToken cancellationToken)
    {
        var items = await products.GetTreesAsync([.. request.Lines.Select(l => new ProductId(l.ProductId)).Distinct()], cancellationToken);
        var byId = items.ToDictionary(i => i.Id.Value);

        // Bundles reserve their leaf products (Composite): a "Cabaz do Mês" takes rice, oil, sugar and flour.
        var leaves = request.Lines
            .Where(l => byId.ContainsKey(l.ProductId))
            .SelectMany(l => byId[l.ProductId].LeafRequirements(l.Quantity))
            .GroupBy(r => r.Product.Id);

        foreach (var leaf in leaves)
        {
            var shortage = leaf.First().Product.Reserve(leaf.Sum(r => r.Quantity));
            if (shortage > 0)
            {
                LogOversold(logger, leaf.First().Product.Sku, shortage, request.OrderNumber);
            }
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Oversold {Sku} by {Shortage} unit(s) for order {OrderNumber}")]
    private static partial void LogOversold(ILogger logger, string sku, int shortage, long orderNumber);
}
