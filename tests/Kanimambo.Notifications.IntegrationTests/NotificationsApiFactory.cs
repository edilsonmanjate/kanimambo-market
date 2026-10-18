using System.Net.Http.Json;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Notifications.IntegrationTests;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.Simulators.Messaging;
using Kanimambo.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(NotificationsApiFactory))]

namespace Kanimambo.Notifications.IntegrationTests;

/// <summary>The real API with a fast outbox and couriers that advance a parcel every 200 ms; messages land in the simulator's inbox.</summary>
public sealed class NotificationsApiFactory : KanimamboWebApplicationFactory
{
    public HttpClient Backoffice => CreateClientAs("ana.backoffice", KnownRoles.Backoffice);

    protected override TimeSpan CourierStepDelay => TimeSpan.FromMilliseconds(200);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.100");
        builder.UseSetting("Outbox:RetryBaseDelay", "00:00:00.200");
        base.ConfigureWebHost(builder);
    }

    public async Task<Guid> CreateProductAsync(decimal price)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var seller = Seller.Register(SellerId.New(), $"Loja {Guid.NewGuid():N}"[..20], "Maputo", 4.5m, ["Maputo"], 0m).Value;
        var product = SimpleProduct.Create(ProductId.New(), seller.Id, new ProductDetails($"P-{Guid.NewGuid():N}"[..30], "Açúcar castanho 1 kg", "mercearia",
            VariantAttributesPool.Shared.Get("pacote", 1m, "kg").Value, Money.Mzn(price), 100, 1m, TaxCategory.From("standard").Value)).Value;
        db.Sellers.Add(seller);
        db.CatalogItems.Add(product);
        await db.SaveChangesAsync();
        return product.Id.Value;
    }

    /// <summary>What a phone number or mailbox received, according to the simulated providers.</summary>
    public async Task<DeliveredMessage[]> InboxAsync(string to) =>
        (await Messaging.CreateClient().GetFromJsonAsync<DeliveredMessage[]>(new Uri($"/inbox?to={Uri.EscapeDataString(to)}", UriKind.Relative), Journey.Ct))!;
}
