using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application.Regimes;

/// <summary>Concrete factory for sellers in the simplified regime (ISPC).</summary>
public sealed class IspcFactory(RegimeOptions options) : IFiscalRegimeFactory
{
    public FiscalRegime Regime => FiscalRegime.Ispc;

    public ITaxCalculator CreateTaxCalculator() => new IspcTaxCalculator(options);

    public IDocumentFormatter CreateFormatter() => new RegimeFormatter(Regime, options);

    public IDocumentSeries CreateSeries() => new RegimeSeries(Regime, options);
}
