using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Search;

/// <summary>
/// Price range expressed in MZN for a catalog priced in several currencies. The MZN bounds are converted
/// into each currency's own bounds with today's rate, so the comparison runs on the stored price in SQL.
/// Items in a currency without an available rate are left out rather than compared wrongly.
/// </summary>
public static class PriceBetweenMzn
{
    public static Specification<CatalogEntry> Create(
        decimal? minimumMzn, decimal? maximumMzn, IReadOnlyDictionary<Currency, decimal> ratesToMzn)
    {
        ArgumentNullException.ThrowIfNull(ratesToMzn);

        Specification<CatalogEntry>? combined = null;
        foreach (var (currency, rate) in ratesToMzn)
        {
            var perCurrency = new PricedInCurrencyBetween(currency, minimumMzn / rate, maximumMzn / rate);
            combined = combined is null ? perCurrency : combined | perCurrency;
        }

        return combined ?? new PricedInCurrencyBetween(Currency.Mzn, minimumMzn, maximumMzn);
    }
}
