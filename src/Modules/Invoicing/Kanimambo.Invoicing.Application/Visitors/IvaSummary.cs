namespace Kanimambo.Invoicing.Application.Visitors;

public sealed record IvaSummary(string IssuerNuit, int Year, int Month, int Invoices, int CreditNotes, IReadOnlyList<IvaSummaryLine> Lines, decimal Net, decimal Iva, decimal Gross);
