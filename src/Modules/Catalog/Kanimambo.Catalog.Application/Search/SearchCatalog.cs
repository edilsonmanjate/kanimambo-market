using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Search;

public sealed record SearchCatalog(
    string? Text = null,
    bool InStockOnly = false,
    decimal? MinPriceMzn = null,
    decimal? MaxPriceMzn = null,
    string? Category = null,
    string? DeliversTo = null,
    decimal? MinSellerRating = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<CatalogEntryDto>>;
