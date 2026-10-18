using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

/// <summary>Wholesalers set a minimum order value; it applies per seller within a multi-seller order.</summary>
public sealed class SellerMinimumsMustBeMet : OrderAcceptanceHandler
{
    protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var belowMinimum = context.Lines
            .GroupBy(l => l.SellerId)
            .Select(g => (Seller: context.Sellers[g.Key], Total: g.Select(l => l.LineTotal).Aggregate(Money.Zero(Currency.Mzn), (a, b) => a + b)))
            .Where(x => x.Total < x.Seller.MinimumOrder)
            .Select(x => $"{x.Seller.Name} (minimum {x.Seller.MinimumOrder}, have {x.Total})")
            .ToList();

        return Task.FromResult(belowMinimum.Count == 0
            ? Result.Success()
            : Result.Failure(Error.BusinessRule("ordering.below_seller_minimum", $"Below the seller minimum: {string.Join("; ", belowMinimum)}.")));
    }
}
