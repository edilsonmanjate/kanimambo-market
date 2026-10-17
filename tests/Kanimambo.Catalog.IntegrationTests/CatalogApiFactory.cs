using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Catalog.IntegrationTests;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.Testing;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(CatalogApiFactory))]

namespace Kanimambo.Catalog.IntegrationTests;

/// <summary>
/// The real API on PostgreSQL + Redis with in-process simulators (see <see cref="KanimamboWebApplicationFactory"/>).
/// </summary>
public sealed class CatalogApiFactory : KanimamboWebApplicationFactory
{
    /// <summary>Creates an isolated seller (and optional products) so tests that mutate data never collide.</summary>
    public async Task<(SellerId Seller, IReadOnlyList<SimpleProduct> Products)> CreateSellerAsync(params (string Sku, decimal Price)[] products)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var seller = Seller.Register(SellerId.New(), $"Loja de teste {Guid.NewGuid():N}", "Matola", 4.5m, ["Matola", "Maputo"]).Value;
        var created = products.Select(p => SimpleProduct.Create(ProductId.New(), seller.Id, new ProductDetails(
            p.Sku, $"Produto {p.Sku}", "testes",
            VariantAttributesPool.Shared.Get("pacote", 1m, "kg").Value,
            Money.Mzn(p.Price), 10, 1m, TaxCategory.From("standard").Value)).Value).ToList();

        db.Sellers.Add(seller);
        db.CatalogItems.AddRange(created);
        await db.SaveChangesAsync();
        return (seller.Id, created);
    }

}
