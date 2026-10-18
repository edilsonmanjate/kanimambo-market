using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

public sealed class SellersMustDeliverToTheCity : OrderAcceptanceHandler
{
    protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var notCovered = context.Lines
            .Select(l => l.SellerId)
            .Distinct()
            .Select(id => context.Sellers.GetValueOrDefault(id))
            .Where(s => s is null || !s.IsActive || !s.DeliveryCities.Contains(context.DeliveryCity, StringComparer.OrdinalIgnoreCase))
            .Select(s => s?.Name ?? "unknown seller")
            .ToList();

        return Task.FromResult(notCovered.Count == 0
            ? Result.Success()
            : Result.Failure(Error.BusinessRule("ordering.delivery_zone_not_covered",
                $"{string.Join(", ", notCovered)} do(es) not deliver to {context.DeliveryCity}.")));
    }
}
