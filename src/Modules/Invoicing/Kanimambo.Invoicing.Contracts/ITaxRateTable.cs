using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Contracts;

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
