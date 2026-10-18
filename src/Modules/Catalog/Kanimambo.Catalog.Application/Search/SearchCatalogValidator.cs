using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Search;

public sealed class SearchCatalogValidator : IValidator<SearchCatalog>
{
    public const int MaxPageSize = 100;

    public IEnumerable<FieldError> Validate(SearchCatalog request)
    {
        if (request.Page < 1)
        {
            yield return new FieldError(nameof(request.Page), "Page starts at 1.");
        }

        if (request.PageSize is < 1 or > MaxPageSize)
        {
            yield return new FieldError(nameof(request.PageSize), $"Page size must be between 1 and {MaxPageSize}.");
        }

        if (request.MinPriceMzn > request.MaxPriceMzn)
        {
            yield return new FieldError(nameof(request.MinPriceMzn), "Minimum price cannot exceed the maximum price.");
        }

        if (request.MinSellerRating is < 0m or > 5m)
        {
            yield return new FieldError(nameof(request.MinSellerRating), "Seller rating is between 0 and 5.");
        }
    }
}
