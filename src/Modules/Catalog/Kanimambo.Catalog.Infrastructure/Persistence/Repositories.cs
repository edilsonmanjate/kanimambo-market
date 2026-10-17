using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Search;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Persistence;

internal sealed class SellerRepository(CatalogDbContext db) : ISellerRepository
{
    public Task<Seller?> GetAsync(SellerId id, CancellationToken cancellationToken) =>
        db.Sellers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Add(Seller seller) => db.Sellers.Add(seller);
}

internal sealed class CatalogItemRepository(CatalogDbContext db) : ICatalogItemRepository
{
    public async Task<CatalogItem?> GetTreeAsync(ProductId id, CancellationToken cancellationToken) =>
        (await GetTreesAsync([id], cancellationToken)).SingleOrDefault();

    /// <summary>
    /// Loads the requested items and then, level by level, the components of every bundle found, until
    /// the leaves. EF's identity map wires each <see cref="BundleComponent.Component"/> as items arrive,
    /// so nested bundles of any depth are fully navigable without a fixed Include depth.
    /// </summary>
    public async Task<IReadOnlyList<CatalogItem>> GetTreesAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken)
    {
        var roots = await LoadAsync(ids, cancellationToken);
        var loaded = roots.Select(r => r.Id).ToHashSet();
        var frontier = roots.OfType<Bundle>().ToList();

        while (frontier.Count > 0)
        {
            var next = frontier
                .SelectMany(b => b.Components.Select(c => c.ComponentId))
                .Where(loaded.Add)
                .ToArray();

            var children = next.Length == 0 ? [] : await LoadAsync(next, cancellationToken);
            frontier = children.OfType<Bundle>().ToList();
        }

        return roots;
    }

    public async Task<IReadOnlyList<SimpleProduct>> GetSimpleProductsAsync(SellerId sellerId, CancellationToken cancellationToken) =>
        await db.CatalogItems.OfType<SimpleProduct>().Where(p => p.SellerId == sellerId).OrderBy(p => p.Sku).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SimpleProduct>> GetSimpleProductsAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken) =>
        await db.CatalogItems.OfType<SimpleProduct>().Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);

    public Task<bool> SkuExistsAsync(SellerId sellerId, string sku, CancellationToken cancellationToken) =>
        db.CatalogItems.AnyAsync(i => i.SellerId == sellerId && i.Sku == sku, cancellationToken);

    public void Add(CatalogItem item) => db.CatalogItems.Add(item);

    private async Task<List<CatalogItem>> LoadAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken) =>
        await db.CatalogItems
            .Include(i => ((Bundle)i).Components)
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(cancellationToken);
}

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

internal sealed class BackofficeOperationLog(CatalogDbContext db) : IBackofficeOperationLog
{
    public void Add(BackofficeOperationRecord record) => db.BackofficeOperations.Add(record);

    public Task<BackofficeOperationRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.BackofficeOperations.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<PagedResult<BackofficeOperationRecord>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await db.BackofficeOperations.CountAsync(cancellationToken);
        var items = await db.BackofficeOperations.AsNoTracking()
            .OrderByDescending(o => o.PerformedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BackofficeOperationRecord>(items, page, pageSize, total);
    }
}
