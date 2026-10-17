using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Search;

/// <summary>Has at least one unit available (for bundles: derived from the components' stock).</summary>
public sealed class InStock : Specification<CatalogEntry>
{
    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.AvailableStock > 0;
}

public sealed class InCategory(string category) : Specification<CatalogEntry>
{
    private readonly string _category = CatalogItem.NormalizeCategory(category);

    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.Category == _category;
}

/// <summary>Case-insensitive search on the product name.</summary>
public sealed class NameContains(string text) : Specification<CatalogEntry>
{
    private readonly string _text = text.Trim().ToLowerInvariant();

    [SuppressMessage("Performance", "CA1862", Justification = "Expression is translated to SQL; StringComparison overloads are not translatable.")]
    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.Name.ToLower().Contains(_text);
}

/// <summary>The seller delivers to the city (e.g. same-day delivery in Matola).</summary>
public sealed class DeliversTo(string city) : Specification<CatalogEntry>
{
    private readonly string _city = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(city.Trim().ToLowerInvariant());

    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.DeliveryCities.Contains(_city);
}

public sealed class SellerRatingAtLeast(decimal minimum) : Specification<CatalogEntry>
{
    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.SellerRating >= minimum;
}

public sealed class PricedInCurrencyBetween(Currency currency, decimal? minimum, decimal? maximum) : Specification<CatalogEntry>
{
    private readonly string _currency = currency.Code;
    private readonly decimal _minimum = minimum ?? decimal.MinValue;
    private readonly decimal _maximum = maximum ?? decimal.MaxValue;

    public override Expression<Func<CatalogEntry, bool>> ToExpression() =>
        e => e.Currency == _currency && e.Price >= _minimum && e.Price <= _maximum;
}

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
