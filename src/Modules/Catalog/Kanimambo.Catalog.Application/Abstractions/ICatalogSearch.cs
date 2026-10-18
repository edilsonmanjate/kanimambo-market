using Kanimambo.Catalog.Application.Search;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Abstractions;

/// <summary>Runs catalog specifications in the database and returns one page of results.</summary>
public interface ICatalogSearch
{
    Task<PagedResult<CatalogEntry>> SearchAsync(
        Specification<CatalogEntry> specification, int page, int pageSize, CancellationToken cancellationToken);
}
