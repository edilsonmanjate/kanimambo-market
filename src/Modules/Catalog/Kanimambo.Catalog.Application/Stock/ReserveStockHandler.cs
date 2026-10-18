using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Catalog.Application.Stock;

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
