using System.Globalization;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application.Regimes;

/// <summary>Formatting is the same shape for both regimes; each factory configures its own instance.</summary>
internal sealed class RegimeFormatter(FiscalRegime regime, RegimeOptions options) : IDocumentFormatter
{
    public FiscalRegime Regime => regime;

    public string? LegalNotice => options.LegalNotice;

    public string Title(DocumentType type) => type == DocumentType.Invoice ? "Fatura" : "Nota de Crédito";

    public string FormatNumber(string series, int year, int number) => string.Create(CultureInfo.InvariantCulture, $"{series} {year}/{number:D6}");
}
