namespace Kanimambo.Invoicing.Domain;

public enum InvoiceRequestStatus
{
    /// <summary>The seller has no fiscal profile yet; the invoice is issued as soon as it registers one.</summary>
    AwaitingTaxpayer,
    Issued,

    /// <summary>The data could not make a valid invoice (e.g. a B2B buyer without a NUIT); needs a human.</summary>
    Rejected,

    /// <summary>The order was refunded before the invoice could be issued.</summary>
    Cancelled,
}
