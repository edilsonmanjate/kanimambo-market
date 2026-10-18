using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Payments.IntegrationTests;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(PaymentsApiFactory))]

namespace Kanimambo.Payments.IntegrationTests;

/// <summary>
/// The real API talking to the in-process mobile-money simulator, with every delay compressed: the simulated
/// customer types the PIN after 300 ms, an unanswered PIN prompt times out after 2 s, and the "delayed callback"
/// scenario answers after 4 s — after the timeout, to exercise late confirmations.
/// </summary>
public sealed class PaymentsApiFactory : KanimamboWebApplicationFactory
{
    public HttpClient Backoffice => CreateClientAs("ana.backoffice", KnownRoles.Backoffice);

    protected override TimeSpan CustomerConfirmationDelay => TimeSpan.FromMilliseconds(300);

    protected override TimeSpan DelayedCallback => TimeSpan.FromSeconds(4);

    protected override TimeSpan PinTimeout => TimeSpan.FromSeconds(2);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.100");
        builder.UseSetting("Outbox:RetryBaseDelay", "00:00:00.200");
        base.ConfigureWebHost(builder);
    }

    /// <summary>A seller of our own with one product, so stock assertions are not disturbed by other tests.</summary>
    public async Task<Guid> CreateProductAsync(decimal price, int stock)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var seller = Seller.Register(SellerId.New(), $"Loja {Guid.NewGuid():N}"[..20], "Maputo", 4.5m, ["Maputo"], 0m).Value;
        var product = SimpleProduct.Create(ProductId.New(), seller.Id, new ProductDetails(
            $"P-{Guid.NewGuid():N}"[..30], "Produto de teste", "mercearia",
            VariantAttributesPool.Shared.Get("pacote", 1m, "kg").Value, Money.Mzn(price), stock, 1m,
            TaxCategory.From("standard").Value)).Value;

        db.Sellers.Add(seller);
        db.CatalogItems.Add(product);
        await db.SaveChangesAsync();
        return product.Id.Value;
    }
}
