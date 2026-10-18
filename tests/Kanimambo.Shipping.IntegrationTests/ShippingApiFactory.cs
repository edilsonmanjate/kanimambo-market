using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.Shipping.IntegrationTests;
using Kanimambo.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(ShippingApiFactory))]

namespace Kanimambo.Shipping.IntegrationTests;

/// <summary>
/// The real API with the courier simulator advancing a parcel every 250 ms (collected, in transit, delivered) and
/// simulated customers confirming mobile-money payments after 300 ms.
/// </summary>
public sealed class ShippingApiFactory : KanimamboWebApplicationFactory
{
    public HttpClient Backoffice => CreateClientAs("ana.backoffice", KnownRoles.Backoffice);

    protected override TimeSpan CourierStepDelay => TimeSpan.FromMilliseconds(250);

    protected override TimeSpan CustomerConfirmationDelay => TimeSpan.FromMilliseconds(300);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.100");
        builder.UseSetting("Outbox:RetryBaseDelay", "00:00:00.200");
        base.ConfigureWebHost(builder);
    }

    /// <summary>A seller of our own based in <paramref name="city"/>, with one product of the given weight.</summary>
    public async Task<(Guid Seller, Guid Product)> CreateSellerWithProductAsync(string city, string[] deliversTo, decimal weightKg, decimal price = 1_000m)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var seller = Seller.Register(SellerId.New(), $"Loja {Guid.NewGuid():N}"[..20], city, 4.5m, deliversTo, 0m).Value;
        var product = SimpleProduct.Create(ProductId.New(), seller.Id, new ProductDetails(
            $"P-{Guid.NewGuid():N}"[..30], "Produto de teste", "mercearia",
            VariantAttributesPool.Shared.Get("saco", weightKg, "kg").Value, Money.Mzn(price), 50, weightKg,
            TaxCategory.From("standard").Value)).Value;

        db.Sellers.Add(seller);
        db.CatalogItems.Add(product);
        await db.SaveChangesAsync();
        return (seller.Id.Value, product.Id.Value);
    }
}
