namespace Kanimambo.Invoicing.Domain;

/// <summary>A fatura. Only <see cref="InvoiceBuilder"/> creates invoices, and only valid ones.</summary>
public sealed class Invoice : FiscalDocument
{
    private Invoice()
    {
    }

    internal Invoice(FiscalRegimeKit kit, int number, DateOnly issueDate, FiscalParty issuer, FiscalParty buyer, Guid orderId, long orderNumber,
        Guid? sellerId, string customerPhone, IEnumerable<FiscalLine> lines, DateTimeOffset now)
        : base(DocumentType.Invoice, kit, number, issueDate, issuer, buyer, orderId, orderNumber, sellerId, customerPhone, lines, now)
    {
    }

    public override void Accept(IFiscalDocumentVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.VisitInvoice(this);
        AcceptLines(visitor);
    }
}
