using System.Globalization;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

/// <summary>
/// Shared walk for the monthly-file visitors: collects each document's lines into tax buckets and hands the finished
/// document to the format. Adding a format (SAF-T later) means a new subclass; the documents never change.
/// </summary>
internal abstract class DeclarationVisitor : IFiscalDocumentVisitor
{
    private readonly Dictionary<(string Category, decimal Rate), (decimal Net, decimal Iva, decimal Gross)> _buckets = [];

    protected static CultureInfo Invariant => CultureInfo.InvariantCulture;

    public virtual void VisitPeriod(FiscalPeriod period)
    {
    }

    public void VisitInvoice(Invoice invoice) => _buckets.Clear();

    public void VisitCreditNote(CreditNote creditNote) => _buckets.Clear();

    public void VisitLine(FiscalDocument document, FiscalLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var key = (line.TaxCategory, line.Rate);
        var current = _buckets.GetValueOrDefault(key);
        _buckets[key] = (current.Net + line.Net, current.Iva + line.Iva, current.Gross + line.Gross);
    }

    public void LeaveDocument(FiscalDocument document) =>
        WriteDocument(document, [.. _buckets.OrderByDescending(b => b.Key.Rate).Select(b => new TaxBucket(b.Key.Category, b.Key.Rate, b.Value.Net, b.Value.Iva, b.Value.Gross))]);

    public virtual void LeavePeriod(FiscalPeriod period)
    {
    }

    protected static string TypeCode(FiscalDocument document) => document.Type == DocumentType.Invoice ? "FT" : "NC";

    protected static string Amount(decimal value) => value.ToString("0.00", Invariant);

    protected abstract void WriteDocument(FiscalDocument document, IReadOnlyList<TaxBucket> buckets);
}
