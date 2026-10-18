namespace Kanimambo.Invoicing.Infrastructure.Documents;

// The monthly file below follows the information the AT's e-Declaração portal asks for when invoices are
// communicated (issuer, document type and number, date, buyer NUIT, taxable base and IVA per rate). The exact
// column layout is our own: check it against the template currently published by the AT before filing.

/// <summary>One document's amounts per (tax category, rate), as the monthly file reports them.</summary>
internal sealed record TaxBucket(string Category, decimal Rate, decimal Net, decimal Iva, decimal Gross);
