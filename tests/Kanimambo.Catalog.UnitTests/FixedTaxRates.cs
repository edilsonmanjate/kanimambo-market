using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.UnitTests;

internal sealed class FixedTaxRates(Dictionary<string, decimal> rates) : ITaxRateTable
{
    public static FixedTaxRates Mozambique { get; } = new(new Dictionary<string, decimal>
    {
        ["standard"] = 0.16m,
        ["reduced"] = 0.05m,
        ["exempt"] = 0m,
    });

    public Result<decimal> RateFor(TaxCategory category) =>
        rates.TryGetValue(category.Code, out var rate) ? rate : Error.BusinessRule("tax.unknown", $"Unknown '{category.Code}'.");
}
