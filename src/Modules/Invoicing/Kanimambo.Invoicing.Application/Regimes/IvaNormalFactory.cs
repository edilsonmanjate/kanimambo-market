using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application.Regimes;

/// <summary>Concrete factory for sellers under IVA normal.</summary>
public sealed class IvaNormalFactory(ITaxRateTable rates, RegimeOptions options) : IFiscalRegimeFactory
{
    public FiscalRegime Regime => FiscalRegime.IvaNormal;

    public ITaxCalculator CreateTaxCalculator() => new IvaTaxCalculator(rates);

    public IDocumentFormatter CreateFormatter() => new RegimeFormatter(Regime, options);

    public IDocumentSeries CreateSeries() => new RegimeSeries(Regime, options);
}
