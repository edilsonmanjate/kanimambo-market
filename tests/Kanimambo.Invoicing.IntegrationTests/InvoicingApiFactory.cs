using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Invoicing.IntegrationTests;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(InvoicingApiFactory))]

namespace Kanimambo.Invoicing.IntegrationTests;

/// <summary>The real API with a fast outbox; payments are confirmed through the back-office to keep tests deterministic.</summary>
public sealed class InvoicingApiFactory : KanimamboWebApplicationFactory
{
    public HttpClient Backoffice => CreateClientAs("ana.backoffice", KnownRoles.Backoffice);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.100");
        builder.UseSetting("Outbox:RetryBaseDelay", "00:00:00.200");
        base.ConfigureWebHost(builder);
    }

    /// <summary>A Maputo seller of our own with products (price includes IVA, tax category per product).</summary>
    public async Task<(Guid Seller, IReadOnlyDictionary<string, Guid> Products)> CreateSellerAsync(params (string Sku, decimal Price, string TaxCategory)[] products)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var seller = Seller.Register(SellerId.New(), $"Loja {Guid.NewGuid():N}"[..20], "Maputo", 4.5m, ["Maputo"], 0m).Value;
        var created = products.ToDictionary(p => p.Sku, p => SimpleProduct.Create(ProductId.New(), seller.Id, new ProductDetails(
            $"{p.Sku}-{Guid.NewGuid():N}"[..30], $"Produto {p.Sku}", "mercearia",
            VariantAttributesPool.Shared.Get("pacote", 1m, "kg").Value, Money.Mzn(p.Price), 100, 1m,
            TaxCategory.From(p.TaxCategory).Value)).Value);

        db.Sellers.Add(seller);
        db.CatalogItems.AddRange(created.Values);
        await db.SaveChangesAsync();
        return (seller.Id.Value, created.ToDictionary(p => p.Key, p => p.Value.Id.Value));
    }
}
