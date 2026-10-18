using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Specifications;
using Kanimambo.SharedKernel.Time;

namespace Kanimambo.Catalog.Application.Search;

/// <summary>Composes the requested filters into one specification, executed by the database.</summary>
public sealed class SearchCatalogHandler(ICatalogSearch search, IExchangeRateProvider exchangeRates, TimeProvider timeProvider)
    : IRequestHandler<SearchCatalog, Result<PagedResult<CatalogEntryDto>>>
{
    public async ValueTask<Result<PagedResult<CatalogEntryDto>>> Handle(SearchCatalog request, CancellationToken cancellationToken)
    {
        var rates = await exchangeRates.GetRatesToMznAsync(MozambiqueTime.Today(timeProvider), cancellationToken);

        var specification = Specification<CatalogEntry>.All;
        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            specification &= new NameContains(request.Text);
        }

        if (request.InStockOnly)
        {
            specification &= new InStock();
        }

        if (request.MinPriceMzn is not null || request.MaxPriceMzn is not null)
        {
            specification &= PriceBetweenMzn.Create(request.MinPriceMzn, request.MaxPriceMzn, rates);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            specification &= new InCategory(request.Category);
        }

        if (!string.IsNullOrWhiteSpace(request.DeliversTo))
        {
            specification &= new DeliversTo(request.DeliversTo);
        }

        if (request.MinSellerRating is { } rating)
        {
            specification &= new SellerRatingAtLeast(rating);
        }

        var page = await search.SearchAsync(specification, request.Page, request.PageSize, cancellationToken);

        var items = page.Items.Select(e => new CatalogEntryDto(
            e.Id, e.Sku, e.Name, e.Category, e.Kind, e.Price, e.Currency,
            ToMzn(e, rates), e.AvailableStock, e.WeightKg,
            e.SellerId, e.SellerName, e.SellerCity, e.SellerRating)).ToList();

        return new PagedResult<CatalogEntryDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private static decimal? ToMzn(CatalogEntry entry, IReadOnlyDictionary<Currency, decimal> rates)
    {
        var currency = Currency.FromCode(entry.Currency);
        return currency.IsSuccess && rates.TryGetValue(currency.Value, out var rate)
            ? Money.Mzn(entry.Price * rate).Amount
            : null;
    }
}
