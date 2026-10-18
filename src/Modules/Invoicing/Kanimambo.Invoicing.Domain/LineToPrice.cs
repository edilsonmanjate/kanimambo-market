namespace Kanimambo.Invoicing.Domain;

/// <summary>A line before tax: what was sold, its category and the amount paid for it (IVA included), before discount.</summary>
public sealed record LineToPrice(string Description, string? Sku, decimal Quantity, string TaxCategory, decimal Gross, decimal Discount = 0m)
{
    public decimal Taxable => Gross - Discount;
}
