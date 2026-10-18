using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Domain;

/// <summary>A nota de crédito: cancels (here, fully) an invoice, for example when an order is refunded.</summary>
public sealed class CreditNote : FiscalDocument
{
    private CreditNote()
    {
    }

    private CreditNote(FiscalRegimeKit kit, int number, DateOnly issueDate, Invoice invoice, string reason, DateTimeOffset now)
        : base(DocumentType.CreditNote, kit, number, issueDate, invoice.Issuer, invoice.Buyer, invoice.OrderId, invoice.OrderNumber,
            invoice.SellerId, invoice.CustomerPhone, invoice.Lines, now)
    {
        CreditedInvoiceId = invoice.Id;
        CreditedInvoiceNumber = invoice.FullNumber;
        Reason = reason;
    }

    public Guid CreditedInvoiceId { get; private set; }

    public string CreditedInvoiceNumber { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    /// <summary>A credit note for the whole invoice, in the series of the invoice's own regime (number taken only when valid).</summary>
    public static Result<CreditNote> ForRefund(Invoice invoice, FiscalRegimeKit kit, Func<int> nextNumber, DateOnly issueDate, string reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(kit);
        if (kit.Regime != invoice.Regime)
        {
            return Error.BusinessRule("invoicing.regime_mismatch", $"Invoice {invoice.FullNumber} is {invoice.Regime}; it cannot be credited with a {kit.Regime} kit.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation(new FieldError("reason", "A credit note needs a reason."));
        }

        ArgumentNullException.ThrowIfNull(nextNumber);
        return new CreditNote(kit, nextNumber(), issueDate, invoice, reason.Trim(), now);
    }

    public override void Accept(IFiscalDocumentVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.VisitCreditNote(this);
        AcceptLines(visitor);
    }
}
