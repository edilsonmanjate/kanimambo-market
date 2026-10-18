using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application.Regimes;

internal sealed class RegimeSeries(FiscalRegime regime, RegimeOptions options) : IDocumentSeries
{
    public FiscalRegime Regime => regime;

    public string For(DocumentType type) => type == DocumentType.Invoice ? options.InvoiceSeries : options.CreditNoteSeries;
}
