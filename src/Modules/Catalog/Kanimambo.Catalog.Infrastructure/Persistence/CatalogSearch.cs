using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Search;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Persistence;

/// <summary>Translates specifications to SQL over the read-model view; only pages of results leave this class.</summary>
internal sealed class CatalogSearch(CatalogDbContext db) : ICatalogSearch
{
    public async Task<PagedResult<CatalogEntry>> SearchAsync(
        Specification<CatalogEntry> specification, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.CatalogEntries.AsNoTracking().Where(specification);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CatalogEntry>(items, page, pageSize, total);
    }
}
