namespace Kanimambo.Invoicing.Application;

public sealed record FiscalLineDto(int LineNumber, string Description, string? Sku, decimal Quantity, string TaxCategory, decimal Rate,
    decimal Discount, decimal Net, decimal Iva, decimal Gross, string? ExemptionReason);
