using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;

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
