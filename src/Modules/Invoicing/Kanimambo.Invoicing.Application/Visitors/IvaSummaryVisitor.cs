using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application.Visitors;

/// <summary>
/// Visitor: IVA per rate for a month — what the issuer owes. Credit notes reduce the amounts. Lines that carry no IVA
/// (exempt, or ISPC) are reported separately by category and rate 0.
/// </summary>
public sealed class IvaSummaryVisitor : IFiscalDocumentVisitor
{
    private readonly Dictionary<(string Category, decimal Rate), (decimal Net, decimal Iva, decimal Gross)> _totals = [];
    private string _issuerNuit = string.Empty;
    private int _year;
    private int _month;
    private int _invoices;
    private int _creditNotes;

    public void VisitPeriod(FiscalPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        (_issuerNuit, _year, _month) = (period.IssuerNuit, period.Year, period.Month);
    }

    public void VisitInvoice(Invoice invoice) => _invoices++;

    public void VisitCreditNote(CreditNote creditNote) => _creditNotes++;

    public void VisitLine(FiscalDocument document, FiscalLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        var sign = document.Type == DocumentType.CreditNote ? -1m : 1m;
        var key = (line.TaxCategory, line.Rate);
        var current = _totals.GetValueOrDefault(key);
        _totals[key] = (current.Net + (sign * line.Net), current.Iva + (sign * line.Iva), current.Gross + (sign * line.Gross));
    }

    public IvaSummary Result()
    {
        var lines = _totals
            .OrderByDescending(t => t.Key.Rate).ThenBy(t => t.Key.Category, StringComparer.Ordinal)
            .Select(t => new IvaSummaryLine(t.Key.Category, t.Key.Rate, t.Value.Net, t.Value.Iva, t.Value.Gross))
            .ToList();
        return new IvaSummary(_issuerNuit, _year, _month, _invoices, _creditNotes, lines, lines.Sum(l => l.Net), lines.Sum(l => l.Iva), lines.Sum(l => l.Gross));
    }
}
