using System.Globalization;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Application.Regimes;

/// <summary>What differs between regimes on paper: series, legal mention, and why ISPC lines carry no IVA.</summary>
public sealed class RegimeOptions
{
    public string InvoiceSeries { get; set; } = "FT";

    public string CreditNoteSeries { get; set; } = "NC";

    public string? LegalNotice { get; set; }

    /// <summary>ISPC only: the mention on every line, since no IVA is charged.</summary>
    public string NotSubjectReason { get; set; } = "Não sujeito a IVA";
}

/// <summary>Formatting is the same shape for both regimes; each factory configures its own instance.</summary>
internal sealed class RegimeFormatter(FiscalRegime regime, RegimeOptions options) : IDocumentFormatter
{
    public FiscalRegime Regime => regime;

    public string? LegalNotice => options.LegalNotice;

    public string Title(DocumentType type) => type == DocumentType.Invoice ? "Fatura" : "Nota de Crédito";

    public string FormatNumber(string series, int year, int number) => string.Create(CultureInfo.InvariantCulture, $"{series} {year}/{number:D6}");
}

internal sealed class RegimeSeries(FiscalRegime regime, RegimeOptions options) : IDocumentSeries
{
    public FiscalRegime Regime => regime;

    public string For(DocumentType type) => type == DocumentType.Invoice ? options.InvoiceSeries : options.CreditNoteSeries;
}

/// <summary>IVA normal: prices include IVA, so each line's IVA is extracted at its category's rate on the issue date.</summary>
internal sealed class IvaTaxCalculator(ITaxRateTable rates) : ITaxCalculator
{
    public FiscalRegime Regime => FiscalRegime.IvaNormal;

    public Result<FiscalLine> Price(int lineNumber, LineToPrice line, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(line);
        var rate = rates.RateFor(line.TaxCategory, on);
        if (rate.IsFailure)
        {
            return rate.Error;
        }

        var gross = line.Taxable;
        var net = Math.Round(gross / (1m + rate.Value.Rate), 2, MidpointRounding.AwayFromZero);
        return new FiscalLine(lineNumber, line.Description, line.Sku, line.Quantity, rate.Value.Category, rate.Value.Rate, line.Discount,
            net, gross - net, gross, rate.Value.Rate == 0m ? rate.Value.ExemptionReason : null);
    }
}

/// <summary>ISPC: the seller does not charge IVA at all; every line says so (the category is kept for reference only).</summary>
internal sealed class IspcTaxCalculator(RegimeOptions options) : ITaxCalculator
{
    public FiscalRegime Regime => FiscalRegime.Ispc;

    public Result<FiscalLine> Price(int lineNumber, LineToPrice line, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new FiscalLine(lineNumber, line.Description, line.Sku, line.Quantity, line.TaxCategory, 0m, line.Discount,
            line.Taxable, 0m, line.Taxable, options.NotSubjectReason);
    }
}

/// <summary>Concrete factory for sellers under IVA normal.</summary>
public sealed class IvaNormalFactory(ITaxRateTable rates, RegimeOptions options) : IFiscalRegimeFactory
{
    public FiscalRegime Regime => FiscalRegime.IvaNormal;

    public ITaxCalculator CreateTaxCalculator() => new IvaTaxCalculator(rates);

    public IDocumentFormatter CreateFormatter() => new RegimeFormatter(Regime, options);

    public IDocumentSeries CreateSeries() => new RegimeSeries(Regime, options);
}

/// <summary>Concrete factory for sellers in the simplified regime (ISPC).</summary>
public sealed class IspcFactory(RegimeOptions options) : IFiscalRegimeFactory
{
    public FiscalRegime Regime => FiscalRegime.Ispc;

    public ITaxCalculator CreateTaxCalculator() => new IspcTaxCalculator(options);

    public IDocumentFormatter CreateFormatter() => new RegimeFormatter(Regime, options);

    public IDocumentSeries CreateSeries() => new RegimeSeries(Regime, options);
}

/// <summary>The registered factories by regime: the issuer's regime picks the whole family, never single components.</summary>
public sealed class FiscalRegimeFactories(IEnumerable<IFiscalRegimeFactory> factories)
{
    private readonly Dictionary<FiscalRegime, FiscalRegimeKit> _kits = factories.ToDictionary(f => f.Regime, FiscalRegimeKit.From);

    public FiscalRegimeKit KitFor(FiscalRegime regime) =>
        _kits.TryGetValue(regime, out var kit) ? kit : throw new InvalidOperationException($"No fiscal regime factory registered for {regime}.");
}
