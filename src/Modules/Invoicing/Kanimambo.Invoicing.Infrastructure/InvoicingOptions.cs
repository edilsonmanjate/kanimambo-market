using Kanimambo.Invoicing.Application.Regimes;
using Kanimambo.Invoicing.Contracts;

namespace Kanimambo.Invoicing.Infrastructure;

/// <summary>The <c>Invoicing</c> section: rates (with their legal history), per-regime series and mentions, the marketplace's own data.</summary>
public sealed class InvoicingOptions
{
    public const string SectionName = "Invoicing";

    public bool SeedDemoData { get; set; }

    public List<TaxRateOptions> TaxRates { get; } = [];

    public string FallbackTaxCategory { get; set; } = "standard";

    public RegimeOptions IvaNormal { get; set; } = new();

    public RegimeOptions Ispc { get; set; } = new() { InvoiceSeries = "FS", CreditNoteSeries = "NS" };

    public MarketplaceOptions Marketplace { get; set; } = new();

    public IEnumerable<TaxRate> Rates() => TaxRates.Select(r => new TaxRate(r.Category, r.Rate, r.ValidFrom, r.ExemptionReason));
}
