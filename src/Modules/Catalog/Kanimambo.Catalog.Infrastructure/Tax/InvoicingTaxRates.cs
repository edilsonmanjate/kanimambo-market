using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;
using RateTable = Kanimambo.Invoicing.Contracts.ITaxRateTable;

namespace Kanimambo.Catalog.Infrastructure.Tax;

/// <summary>
/// The catalog's IVA rates, read from the Invoicing module's rate table (one cached instance for the whole
/// application, with the legal history of every rate) at today's date in Maputo.
/// </summary>
internal sealed class InvoicingTaxRates(RateTable rates, TimeProvider timeProvider) : ITaxRateTable
{
    public Result<decimal> RateFor(TaxCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        var rate = rates.RateFor(category.Code, MozambiqueTime.Today(timeProvider));
        return rate.IsSuccess ? rate.Value.Rate : rate.Error;
    }
}
