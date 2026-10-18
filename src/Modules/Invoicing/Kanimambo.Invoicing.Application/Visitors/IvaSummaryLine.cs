namespace Kanimambo.Invoicing.Application.Visitors;

public sealed record IvaSummaryLine(string TaxCategory, decimal Rate, decimal Net, decimal Iva, decimal Gross);
