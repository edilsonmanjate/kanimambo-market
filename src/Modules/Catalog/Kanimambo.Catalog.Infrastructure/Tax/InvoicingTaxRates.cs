using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Kanimambo.Catalog.Infrastructure.Tax;

public sealed class TaxRatesOptions
{
    public const string SectionName = "Catalog:TaxRates";

    /// <summary>Rate per tax category code, as a fraction (0.16 = 16 %).</summary>
    public Dictionary<string, decimal> Rates { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Reads rates from configuration because they change by law. Phase 6 replaces this with the Invoicing
/// module's cached tax table (Singleton), which also knows the ISPC regime.
/// </summary>
internal sealed class ConfiguredTaxRateTable(IOptions<TaxRatesOptions> options) : ITaxRateTable
{
    public Result<decimal> RateFor(TaxCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return options.Value.Rates.TryGetValue(category.Code, out var rate)
            ? rate
            : Error.BusinessRule("catalog.unknown_tax_category", $"No tax rate configured for category '{category.Code}'.");
    }
}
