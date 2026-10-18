using System.Globalization;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

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
