using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Contracts;

/// <summary>
/// An IVA rate for a tax category, valid from a date (rates change by law: the standard rate went from 17 % to
/// 16 % in 2023). Exempt categories carry the legal reason that must be printed on the invoice.
/// </summary>
public sealed record TaxRate(string Category, decimal Rate, DateOnly ValidFrom, string? ExemptionReason);

/// <summary>
/// The IVA rate table, loaded from configuration once and cached for the whole application (a DI singleton).
/// The Catalog uses it for its IVA breakdown; Invoicing for fiscal documents.
/// </summary>
public interface ITaxRateTable
{
    /// <summary>The rate in force on <paramref name="on"/> for the category, or <c>tax.unknown</c>.</summary>
    Result<TaxRate> RateFor(string category, DateOnly on);

    /// <summary>Every category's rate in force on the date.</summary>
    IReadOnlyList<TaxRate> RatesOn(DateOnly on);
}

/// <summary>An invoice or credit note was issued (Notifications sends it to the customer in Phase 7).</summary>
public sealed record FiscalDocumentIssuedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid DocumentId,
    string DocumentType,
    string Number,
    Guid OrderId,
    long OrderNumber,
    string IssuerNuit,
    string IssuerName,
    decimal TotalMzn,
    string CustomerPhone) : IIntegrationEvent;
