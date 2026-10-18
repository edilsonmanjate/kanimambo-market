namespace Kanimambo.Invoicing.Contracts;

/// <summary>
/// An IVA rate for a tax category, valid from a date (rates change by law: the standard rate went from 17 % to
/// 16 % in 2023). Exempt categories carry the legal reason that must be printed on the invoice.
/// </summary>
public sealed record TaxRate(string Category, decimal Rate, DateOnly ValidFrom, string? ExemptionReason);
