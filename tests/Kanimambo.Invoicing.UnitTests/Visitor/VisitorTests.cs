using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClosedXML.Excel;
using Kanimambo.Invoicing.Application.Visitors;
using Kanimambo.Invoicing.Domain;
using Kanimambo.Invoicing.Infrastructure.Documents;
using static Kanimambo.Invoicing.UnitTests.TestFiscal;

namespace Kanimambo.Invoicing.UnitTests.Visitor;

/// <summary>Several outputs from the same document tree; none of them changes the documents.</summary>
public sealed partial class VisitorTests
{
    private static FiscalPeriod Period()
    {
        var first = Invoice(number: 1);
        var second = Valid().AddLine(new LineToPrice("Painel solar", "SOLAR", 1m, "reduced", 2_100m)).Build(() => 2, Now).Value;
        var credit = CreditNote.ForRefund(first, IvaKit(), () => 1, Today, "Encomenda reembolsada", Now).Value;
        return new FiscalPeriod(Seller.Nuit!, Seller.Name, 2026, 10, [second, credit, first]);
    }

    [Fact]
    public void The_IVA_summary_adds_invoices_and_subtracts_credit_notes_per_rate()
    {
        var visitor = new IvaSummaryVisitor();

        Period().Accept(visitor);

        var summary = visitor.Result();
        (summary.Invoices, summary.CreditNotes).ShouldBe((2, 1));
        summary.Lines.Select(l => (l.TaxCategory, l.Rate, l.Net, l.Iva)).ShouldBe([
            ("standard", 0.16m, 862.07m, 137.93m),  // two invoices with it, one credited
            ("reduced", 0.05m, 2_000m, 100m),
            ("exempt", 0m, 1_850m, 0m),
        ]);
        summary.Iva.ShouldBe(237.93m);
    }

    [Fact]
    public void The_csv_monthly_file_has_one_row_per_document_and_rate()
    {
        var csv = new StringBuilder();

        Period().Accept(new EDeclaracaoCsvVisitor(csv));

        var rows = csv.ToString().TrimEnd('\n').Split('\n');
        rows[0].ShouldStartWith("NuitEmitente;TipoDocumento;NumeroDocumento");
        rows.Length.ShouldBe(1 + 2 + 3 + 2);
        rows.ShouldContain("400100002;FT;FT 2026/000002;2026-10-19;999999999;Consumidor final;;reduced;0.05;2000.00;100.00;2100.00");
        rows.ShouldContain(r => r.StartsWith("400100002;NC;NC 2026/000001;", StringComparison.Ordinal) && r.Contains(";FT 2026/000001;", StringComparison.Ordinal));
    }

    [Fact]
    public void The_xml_monthly_file_is_well_formed_and_complete()
    {
        var xml = new StringBuilder();

        Period().Accept(new EDeclaracaoXmlVisitor(xml));

        var root = XDocument.Parse(xml.ToString()).Root!;
        root.Attribute("nuitEmitente")!.Value.ShouldBe("400100002");
        root.Elements("Documento").Count().ShouldBe(3);
        root.Elements("Documento").Single(d => d.Attribute("tipo")!.Value == "NC").Attribute("documentoOrigem")!.Value.ShouldBe("FT 2026/000001");
    }

    [Fact]
    public void The_xlsx_monthly_file_opens_in_Excel_with_every_row()
    {
        using var visitor = new EDeclaracaoXlsxVisitor();
        Period().Accept(visitor);

        using var workbook = new XLWorkbook(new MemoryStream(visitor.ToArray()));
        var sheet = workbook.Worksheet("2026-10");
        sheet.LastRowUsed()!.RowNumber().ShouldBe(1 + 7);
        sheet.Cell(1, 10).GetString().ShouldBe("BaseTributavel");
    }

    [Fact]
    public void The_pdf_is_a_valid_document_with_the_regimes_title_and_notice()
    {
        using var writer = new PdfWriter();
        Invoice(IspcKit()).Accept(new InvoicePdfVisitor(writer));

        var bytes = writer.ToArray();
        var text = Encoding.Latin1.GetString(bytes);
        text.ShouldStartWith("%PDF-1.4");
        text.TrimEnd().ShouldEndWith("%%EOF");
        text.ShouldContain("(Fatura FS 2026/000001)");
        text.ShouldContain("Regime do ISPC.");
        PdfIsConsistent(bytes);
    }

    [Fact]
    public void A_new_output_is_a_new_visitor_and_the_documents_do_not_change()
    {
        var counter = new LineCounter();

        Period().Accept(counter);

        counter.Lines.ShouldBe(2 + 3 + 2);
    }

    /// <summary>Every xref offset must point at "N 0 obj": the file is readable by any PDF viewer.</summary>
    private static void PdfIsConsistent(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var start = int.Parse(StartXref().Match(text).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var entries = XrefEntry().Matches(text[start..]);
        entries.Count.ShouldBeGreaterThan(4);
        for (var i = 0; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            text[offset..].ShouldStartWith($"{i + 1} 0 obj");
        }
    }

    [GeneratedRegex(@"startxref\n(\d+)")]
    private static partial Regex StartXref();

    [GeneratedRegex(@"(\d{10}) 00000 n")]
    private static partial Regex XrefEntry();

    private sealed class LineCounter : IFiscalDocumentVisitor
    {
        public int Lines { get; private set; }

        public void VisitInvoice(Invoice invoice)
        {
        }

        public void VisitCreditNote(CreditNote creditNote)
        {
        }

        public void VisitLine(FiscalDocument document, FiscalLine line) => Lines++;
    }
}
