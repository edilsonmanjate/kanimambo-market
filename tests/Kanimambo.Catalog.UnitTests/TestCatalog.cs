using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.UnitTests;

/// <summary>Builders for catalog test data.</summary>
internal static class TestCatalog
{
    public static readonly SellerId Mercearia = new(Guid.Parse("00000000-0000-7000-8000-00000000aaaa"));
    public static readonly SellerId OtherSeller = new(Guid.Parse("00000000-0000-7000-8000-00000000bbbb"));

    public static SimpleProduct Product(
        string sku,
        decimal price,
        int stock = 100,
        decimal weightKg = 1m,
        string taxCategory = "standard",
        SellerId? seller = null,
        Currency? currency = null) =>
        SimpleProduct.Create(ProductId.New(), seller ?? Mercearia, Details(sku, price, stock, weightKg, taxCategory, currency)).Value;

    public static ProductDetails Details(string sku, decimal price, int stock = 100, decimal weightKg = 1m, string taxCategory = "standard", Currency? currency = null) =>
        new(sku, $"Produto {sku}", "mercearia",
            VariantAttributesPool.Shared.Get("pacote", 1m, "kg").Value,
            Money.Of(price, currency ?? Currency.Mzn),
            stock, weightKg,
            TaxCategory.From(taxCategory).Value);

    public static Bundle Bundle(string sku, params (CatalogItem Item, int Quantity)[] components) =>
        Domain.Products.Bundle.Create(ProductId.New(), Mercearia, sku, $"Cabaz {sku}", "cabazes", components).Value;

    public static Seller Seller(SellerId? id = null, string name = "Mercearia Xipamanine") =>
        Domain.Sellers.Seller.Register(id ?? Mercearia, name, "Maputo", 4.5m, ["Maputo", "Matola"]).Value;
}

internal sealed class FixedTaxRates(Dictionary<string, decimal> rates) : ITaxRateTable
{
    public static FixedTaxRates Mozambique { get; } = new(new Dictionary<string, decimal>
    {
        ["standard"] = 0.16m,
        ["reduced"] = 0.05m,
        ["exempt"] = 0m,
    });

    public Result<decimal> RateFor(TaxCategory category) =>
        rates.TryGetValue(category.Code, out var rate) ? rate : Error.BusinessRule("tax.unknown", $"Unknown '{category.Code}'.");
}

internal sealed class InMemorySellers : ISellerRepository
{
    private readonly Dictionary<SellerId, Seller> _sellers = [];

    public InMemorySellers(params Seller[] sellers)
    {
        foreach (var seller in sellers)
        {
            Add(seller);
        }
    }

    public Task<Seller?> GetAsync(SellerId id, CancellationToken cancellationToken) => Task.FromResult(_sellers.GetValueOrDefault(id));

    public void Add(Seller seller) => _sellers[seller.Id] = seller;
}

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
