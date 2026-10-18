using ClosedXML.Excel;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

/// <summary>Monthly file as an Excel workbook (ClosedXML; not pooled — the workbook is the expensive part and is not reusable).</summary>
internal sealed class EDeclaracaoXlsxVisitor : DeclarationVisitor, IDisposable
{
    private readonly XLWorkbook _workbook = new();
    private IXLWorksheet _sheet = null!;
    private int _row = 1;

    public override void VisitPeriod(FiscalPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        _sheet = _workbook.AddWorksheet(string.Create(Invariant, $"{period.Year}-{period.Month:D2}"));
        string[] headers = ["NuitEmitente", "TipoDocumento", "NumeroDocumento", "DataEmissao", "NuitAdquirente", "NomeAdquirente", "DocumentoOrigem",
            "CategoriaFiscal", "Taxa", "BaseTributavel", "Iva", "Total"];
        for (var i = 0; i < headers.Length; i++)
        {
            _sheet.Cell(1, i + 1).Value = headers[i];
        }

        _sheet.Row(1).Style.Font.Bold = true;
    }

    public byte[] ToArray()
    {
        _sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        _workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public void Dispose() => _workbook.Dispose();

    protected override void WriteDocument(FiscalDocument document, IReadOnlyList<TaxBucket> buckets)
    {
        foreach (var bucket in buckets)
        {
            _row++;
            var r = _sheet.Row(_row);
            r.Cell(1).Value = document.IssuerNuit;
            r.Cell(2).Value = TypeCode(document);
            r.Cell(3).Value = document.FullNumber;
            r.Cell(4).Value = document.IssueDate.ToDateTime(TimeOnly.MinValue);
            r.Cell(5).Value = document.Buyer.Nuit ?? "999999999";
            r.Cell(6).Value = document.Buyer.Name;
            r.Cell(7).Value = (document as CreditNote)?.CreditedInvoiceNumber ?? string.Empty;
            r.Cell(8).Value = bucket.Category;
            r.Cell(9).Value = bucket.Rate;
            r.Cell(10).Value = bucket.Net;
            r.Cell(11).Value = bucket.Iva;
            r.Cell(12).Value = bucket.Gross;
        }
    }
}
