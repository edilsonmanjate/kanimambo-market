namespace Kanimambo.Invoicing.Domain;

/// <summary>
/// One priced line of a fiscal document, all amounts in MZN. Prices include IVA: <see cref="Gross"/> is what the
/// customer pays for the line after <see cref="Discount"/>, and <see cref="Net"/> + <see cref="Iva"/> = <see cref="Gross"/>.
/// </summary>
public sealed record FiscalLine(
    int LineNumber,
    string Description,
    string? Sku,
    decimal Quantity,
    string TaxCategory,
    decimal Rate,
    decimal Discount,
    decimal Net,
    decimal Iva,
    decimal Gross,
    string? ExemptionReason);
