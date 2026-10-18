using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;

namespace Kanimambo.Catalog.UnitTests;

internal sealed class InMemoryCatalogItems : ICatalogItemRepository
{
    private readonly List<CatalogItem> _items = [];

    public InMemoryCatalogItems(params CatalogItem[] items) => _items.AddRange(items);

    public Task<CatalogItem?> GetTreeAsync(ProductId id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Find(i => i.Id == id));

    public Task<IReadOnlyList<CatalogItem>> GetTreesAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CatalogItem>>(_items.Where(i => ids.Contains(i.Id)).ToList());

    public Task<IReadOnlyList<SimpleProduct>> GetSimpleProductsAsync(SellerId sellerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SimpleProduct>>(_items.OfType<SimpleProduct>().Where(p => p.SellerId == sellerId).ToList());

    public Task<IReadOnlyList<SimpleProduct>> GetSimpleProductsAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SimpleProduct>>(_items.OfType<SimpleProduct>().Where(p => ids.Contains(p.Id)).ToList());

    public Task<bool> SkuExistsAsync(SellerId sellerId, string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Exists(i => i.SellerId == sellerId && i.Sku == sku));

    public void Add(CatalogItem item) => _items.Add(item);
}
