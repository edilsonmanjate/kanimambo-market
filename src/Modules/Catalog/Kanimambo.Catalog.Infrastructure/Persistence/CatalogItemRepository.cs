using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Persistence;

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
