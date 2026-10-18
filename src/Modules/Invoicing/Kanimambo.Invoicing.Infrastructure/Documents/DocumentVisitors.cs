using System.Globalization;
using System.Security;
using System.Text;
using ClosedXML.Excel;
using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

// The monthly file below follows the information the AT's e-Declaração portal asks for when invoices are
// communicated (issuer, document type and number, date, buyer NUIT, taxable base and IVA per rate). The exact
// column layout is our own: check it against the template currently published by the AT before filing.

/// <summary>One document's amounts per (tax category, rate), as the monthly file reports them.</summary>
internal sealed record TaxBucket(string Category, decimal Rate, decimal Net, decimal Iva, decimal Gross);

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

/// <summary>Monthly file as CSV (semicolon separated, one row per document and tax rate).</summary>
internal sealed class EDeclaracaoCsvVisitor(StringBuilder output) : DeclarationVisitor
{
    public override void VisitPeriod(FiscalPeriod period) =>
        output.Append("NuitEmitente;TipoDocumento;NumeroDocumento;DataEmissao;NuitAdquirente;NomeAdquirente;DocumentoOrigem;CategoriaFiscal;Taxa;BaseTributavel;Iva;Total\n");

    protected override void WriteDocument(FiscalDocument document, IReadOnlyList<TaxBucket> buckets)
    {
        foreach (var bucket in buckets)
        {
            output.Append(document.IssuerNuit).Append(';')
                .Append(TypeCode(document)).Append(';')
                .Append(document.FullNumber).Append(';')
                .Append(document.IssueDate.ToString("yyyy-MM-dd", Invariant)).Append(';')
                .Append(document.Buyer.Nuit ?? "999999999").Append(';')
                .Append(Clean(document.Buyer.Name)).Append(';')
                .Append((document as CreditNote)?.CreditedInvoiceNumber).Append(';')
                .Append(bucket.Category).Append(';')
                .Append(bucket.Rate.ToString("0.00", Invariant)).Append(';')
                .Append(Amount(bucket.Net)).Append(';')
                .Append(Amount(bucket.Iva)).Append(';')
                .Append(Amount(bucket.Gross)).Append('\n');
        }
    }

    private static string Clean(string value) => value.Replace(';', ',').Replace('\n', ' ');
}

/// <summary>Monthly file as XML.</summary>
internal sealed class EDeclaracaoXmlVisitor(StringBuilder output) : DeclarationVisitor
{
    public override void VisitPeriod(FiscalPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        output.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n")
            .Append(CultureInfo.InvariantCulture, $"<ComunicacaoFaturas nuitEmitente=\"{period.IssuerNuit}\" nome=\"{Escape(period.IssuerName)}\" ano=\"{period.Year}\" mes=\"{period.Month:D2}\">\n");
    }

    public override void LeavePeriod(FiscalPeriod period) => output.Append("</ComunicacaoFaturas>\n");

    protected override void WriteDocument(FiscalDocument document, IReadOnlyList<TaxBucket> buckets)
    {
        output.Append(Invariant, $"  <Documento tipo=\"{TypeCode(document)}\" numero=\"{Escape(document.FullNumber)}\" data=\"{document.IssueDate:yyyy-MM-dd}\"")
            .Append(Invariant, $" nuitAdquirente=\"{document.Buyer.Nuit ?? "999999999"}\" nomeAdquirente=\"{Escape(document.Buyer.Name)}\" total=\"{Amount(document.Gross)}\"");
        if (document is CreditNote credit)
        {
            output.Append(Invariant, $" documentoOrigem=\"{Escape(credit.CreditedInvoiceNumber)}\"");
        }

        output.Append(">\n");
        foreach (var bucket in buckets)
        {
            output.Append(Invariant, $"    <Imposto categoria=\"{Escape(bucket.Category)}\" taxa=\"{bucket.Rate.ToString("0.00", Invariant)}\" base=\"{Amount(bucket.Net)}\" iva=\"{Amount(bucket.Iva)}\" />\n");
        }

        output.Append("  </Documento>\n");
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}

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

/// <summary>Visitor: renders one fiscal document as a PDF page (or pages) with the regime's title and legal mention.</summary>
internal sealed class InvoicePdfVisitor(PdfWriter pdf) : IFiscalDocumentVisitor
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public void VisitInvoice(Invoice invoice) => Header(invoice, null);

    public void VisitCreditNote(CreditNote creditNote)
    {
        ArgumentNullException.ThrowIfNull(creditNote);
        Header(creditNote, $"Anula a fatura {creditNote.CreditedInvoiceNumber}. Motivo: {creditNote.Reason}");
    }

    public void VisitLine(FiscalDocument document, FiscalLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        pdf.Text(50, Truncate(line.Description, 52)).Text(330, line.Quantity.ToString("0.###", Invariant))
            .Text(370, string.Create(Invariant, $"{line.Rate * 100:0.#}%")).Text(410, Mzn(line.Net)).Text(470, Mzn(line.Iva)).Text(530, Mzn(line.Gross));
        pdf.NewLine();
        if (line.ExemptionReason is { } reason)
        {
            pdf.Text(60, Truncate(reason, 90), 7f).NewLine(11f);
        }
    }

    public void LeaveDocument(FiscalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        pdf.Rule();
        pdf.Text(350, "Total sem IVA").Text(500, Mzn(document.Net)).NewLine();
        pdf.Text(350, "IVA").Text(500, Mzn(document.Iva)).NewLine();
        pdf.Text(350, "Total a pagar (MZN)", 10f, bold: true).Text(500, Mzn(document.Gross), 10f, bold: true).NewLine(20f);
        if (document.LegalNotice is { } notice)
        {
            pdf.Text(50, notice, 8f).NewLine();
        }

        pdf.Text(50, "Processado por computador — Kanimambo Market", 7f).NewLine();
    }

    private void Header(FiscalDocument d, string? note)
    {
        pdf.Text(50, $"{d.Title} {d.FullNumber}", 16f, bold: true).NewLine(22f);
        pdf.Text(50, d.Issuer.Name, 10f, bold: true).NewLine();
        pdf.Text(50, $"NUIT {d.Issuer.Nuit}").Text(300, $"Data de emissão: {d.IssueDate:dd/MM/yyyy}").NewLine();
        if (d.Issuer.Address is { } address)
        {
            pdf.Text(50, address).NewLine();
        }

        pdf.NewLine(6f);
        pdf.Text(50, $"Cliente: {d.Buyer.Name}").Text(300, $"NUIT: {d.Buyer.Nuit ?? "—"}").NewLine();
        pdf.Text(50, $"Encomenda KM-{d.OrderNumber}").NewLine();
        if (note is not null)
        {
            pdf.Text(50, note).NewLine();
        }

        pdf.NewLine(6f);
        pdf.Text(50, "Descrição", bold: true).Text(330, "Qtd", bold: true).Text(370, "IVA", bold: true)
            .Text(410, "Base", bold: true).Text(470, "IVA", bold: true).Text(530, "Total", bold: true).NewLine();
        pdf.Rule();
    }

    private static string Mzn(decimal value) => value.ToString("#,##0.00", Invariant);

    private static string Truncate(string value, int max) => value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 3), "...");
}

/// <summary>Picks the visitor by format from a table (no switch) and borrows pooled buffers for the text formats.</summary>
internal sealed class MonthlyDeclarationExporter : IMonthlyDeclarationExporter
{
    private readonly Dictionary<string, Func<FiscalPeriod, ExportedFile>> _formats;
    private readonly InvoicingPools _pools;

    public MonthlyDeclarationExporter(InvoicingPools pools)
    {
        _pools = pools;
        _formats = new(StringComparer.OrdinalIgnoreCase)
        {
            ["csv"] = p => Text(p, sb => new EDeclaracaoCsvVisitor(sb), "csv", "text/csv"),
            ["xml"] = p => Text(p, sb => new EDeclaracaoXmlVisitor(sb), "xml", "application/xml"),
            ["xlsx"] = Xlsx,
        };
    }

    public IReadOnlyCollection<string> Formats => _formats.Keys;

    public Result<ExportedFile> Export(FiscalPeriod period, string format) =>
        _formats.TryGetValue(format, out var export)
            ? export(period)
            : Error.Validation(new FieldError("format", $"Unknown format '{format}'. Use one of: {string.Join(", ", _formats.Keys)}."));

    private static string FileName(FiscalPeriod p, string extension) =>
        string.Create(CultureInfo.InvariantCulture, $"e-declaracao-{p.IssuerNuit}-{p.Year}-{p.Month:D2}.{extension}");

    private ExportedFile Text(FiscalPeriod period, Func<StringBuilder, IFiscalDocumentVisitor> visitor, string extension, string contentType)
    {
        var buffer = _pools.StringBuilders.Get();
        try
        {
            period.Accept(visitor(buffer));
            return new ExportedFile(FileName(period, extension), contentType, Encoding.UTF8.GetBytes(buffer.ToString()));
        }
        finally
        {
            _pools.StringBuilders.Return(buffer);
        }
    }

    private static ExportedFile Xlsx(FiscalPeriod period)
    {
        using var visitor = new EDeclaracaoXlsxVisitor();
        period.Accept(visitor);
        return new ExportedFile(FileName(period, "xlsx"), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", visitor.ToArray());
    }
}

internal sealed class FiscalDocumentPdfRenderer(InvoicingPools pools) : IFiscalDocumentPdfRenderer
{
    public ExportedFile Render(FiscalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var writer = pools.PdfWriters.Get();
        try
        {
            document.Accept(new InvoicePdfVisitor(writer));
            return new ExportedFile($"{document.FullNumber.Replace(' ', '-').Replace('/', '-')}.pdf", "application/pdf", writer.ToArray());
        }
        finally
        {
            pools.PdfWriters.Return(writer);
        }
    }
}
