using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Ordering.Contracts;
using Kanimambo.Ordering.IntegrationTests;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(OrderingApiFactory))]

namespace Kanimambo.Ordering.IntegrationTests;

/// <summary>
/// The real API with a fast outbox (100 ms polling, 3 attempts) and one extra, controllable subscriber of
/// <see cref="OrderPaidIntegrationEvent"/> that can be told to fail for a given order.
/// </summary>
public sealed class OrderingApiFactory : KanimamboWebApplicationFactory
{
    public FailurePlan Failures { get; } = new();

    public HttpClient Backoffice => CreateClientAs("ana.backoffice", KnownRoles.Backoffice);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.100");
        builder.UseSetting("Outbox:RetryBaseDelay", "00:00:00.200");
        builder.UseSetting("Outbox:MaxAttempts", "3");
        base.ConfigureWebHost(builder);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(Failures);
        services.AddScoped<INotificationHandler<OrderPaidIntegrationEvent>, FlakyOrderPaidSubscriber>();
    }

    /// <summary>A seller of our own, so stock assertions are not disturbed by other tests.</summary>
    public async Task<(Guid Seller, IReadOnlyDictionary<string, Guid> Products)> CreateSellerAsync(
        decimal minimumOrder, string[] deliveryCities, params (string Sku, decimal Price, int Stock)[] products)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var seller = Seller.Register(SellerId.New(), $"Loja {Guid.NewGuid():N}"[..20], deliveryCities[0], 4.5m, deliveryCities, minimumOrder).Value;
        var created = products.ToDictionary(p => p.Sku, p => SimpleProduct.Create(ProductId.New(), seller.Id, new ProductDetails(
            $"{p.Sku}-{Guid.NewGuid():N}"[..30], $"Produto {p.Sku}", "mercearia",
            VariantAttributesPool.Shared.Get("pacote", 1m, "kg").Value, Money.Mzn(p.Price), p.Stock, 1m,
            TaxCategory.From("standard").Value)).Value);

        db.Sellers.Add(seller);
        db.CatalogItems.AddRange(created.Values);
        await db.SaveChangesAsync();
        return (seller.Id.Value, created.ToDictionary(p => p.Key, p => p.Value.Id.Value));
    }

    public async Task SetStockAsync(Guid productId, int stock)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.\"CatalogItems\" SET \"Stock\" = {stock} WHERE \"Id\" = {productId}");
    }
}
