namespace Kanimambo.Invoicing.Domain;

/// <summary>
/// Visitor over the fiscal document tree (period → documents → lines). Each output — the monthly e-Declaração file
/// in CSV, XLSX or XML, the invoice PDF, the IVA summary, and later SAF-T — is one visitor; the documents never change.
/// </summary>
public interface IFiscalDocumentVisitor
{
    void VisitPeriod(FiscalPeriod period)
    {
    }

    void VisitInvoice(Invoice invoice);

    void VisitCreditNote(CreditNote creditNote);

    void VisitLine(FiscalDocument document, FiscalLine line);

    void LeaveDocument(FiscalDocument document)
    {
    }

    void LeavePeriod(FiscalPeriod period)
    {
    }
}
