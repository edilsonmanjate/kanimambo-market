using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Pricing;

public sealed record IvaLine(string Category, decimal Rate, Money Net, Money Iva);

public sealed record IvaBreakdown(IReadOnlyList<IvaLine> Lines, Money Net, Money Iva)
{
    public Money Gross => Net + Iva;

    /// <summary>
    /// Groups the composite's taxable amounts by category and applies each category's configured rate.
    /// Used for display; the fiscal document calculation lives in Invoicing (Phase 6).
    /// </summary>
    public static Result<IvaBreakdown> Calculate(IEnumerable<TaxableAmount> amounts, ITaxRateTable rates)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        ArgumentNullException.ThrowIfNull(rates);

        var lines = new List<IvaLine>();
        foreach (var group in amounts.GroupBy(a => a.Category).OrderBy(g => g.Key.Code, StringComparer.Ordinal))
        {
            var rate = rates.RateFor(group.Key);
            if (rate.IsFailure)
            {
                return rate.Error;
            }

            var net = group.Select(a => a.Net).Aggregate((total, next) => total + next);
            lines.Add(new IvaLine(group.Key.Code, rate.Value, net, net * rate.Value));
        }

        if (lines.Count == 0)
        {
            return Error.Failure("catalog.no_taxable_amounts", "Nothing to tax.");
        }

        return new IvaBreakdown(
            lines,
            lines.Select(l => l.Net).Aggregate((total, next) => total + next),
            lines.Select(l => l.Iva).Aggregate((total, next) => total + next));
    }
}
