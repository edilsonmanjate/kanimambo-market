using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;

namespace Kanimambo.Catalog.Application.Abstractions;

/// <summary>Repositories return materialized aggregates; <see cref="IQueryable"/> never leaves Infrastructure.</summary>
public interface ICatalogItemRepository
{
    /// <summary>Loads the item and every descendant, so Composite operations can recurse safely.</summary>
    Task<CatalogItem?> GetTreeAsync(ProductId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogItem>> GetTreesAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<SimpleProduct>> GetSimpleProductsAsync(SellerId sellerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SimpleProduct>> GetSimpleProductsAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(SellerId sellerId, string sku, CancellationToken cancellationToken);

    void Add(CatalogItem item);
}
