using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

public sealed class StockMustBeAvailable : OrderAcceptanceHandler
{
    protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var shortLines = context.Lines.Where(l => l.Quantity > l.AvailableStock).Select(l => $"{l.Sku} ({l.AvailableStock} left)").ToList();
        return Task.FromResult(shortLines.Count == 0
            ? Result.Success()
            : Result.Failure(Error.BusinessRule("ordering.insufficient_stock", $"Not enough stock: {string.Join(", ", shortLines)}.")));
    }
}
