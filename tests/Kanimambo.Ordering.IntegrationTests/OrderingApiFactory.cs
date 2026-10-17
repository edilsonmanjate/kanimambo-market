using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using Kanimambo.SharedKernel.Web.Idempotency;
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

/// <summary>Stands in for a not-yet-built subscriber (e.g. invoicing) that can fail on demand.</summary>
public sealed class FlakyOrderPaidSubscriber(FailurePlan plan) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public ValueTask Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        plan.Calls.AddOrUpdate(notification.OrderId, 1, (_, n) => n + 1);

        if (plan.FailAlways.ContainsKey(notification.OrderId)
            || (plan.FailTimes.TryGetValue(notification.OrderId, out var left) && left > 0 && plan.FailTimes.TryUpdate(notification.OrderId, left - 1, left)))
        {
            throw new InvalidOperationException($"Simulated outage for order {notification.OrderNumber}");
        }

        return ValueTask.CompletedTask;
    }
}

public sealed class FailurePlan
{
    public ConcurrentDictionary<Guid, int> FailTimes { get; } = new();

    public ConcurrentDictionary<Guid, bool> FailAlways { get; } = new();

    public ConcurrentDictionary<Guid, int> Calls { get; } = new();
}

/// <summary>The customer journey over HTTP.</summary>
internal static class Journey
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Last four digits >= 1000: the mobile-money simulator reserves 0001-0005 for failure scenarios.
    public static string NewPhone() => $"25884{Random.Shared.Next(100, 999)}{Random.Shared.Next(1_000, 9_999)}";

    public static async Task<Guid> CartWithAsync(this HttpClient client, string city, params (Guid Product, int Quantity)[] lines)
    {
        var cart = await client.PostAsJsonAsync("/api/carts", new { deliveryCity = city }, Ct).Json(HttpStatusCode.Created);
        var id = cart.GetProperty("id").GetGuid();
        foreach (var (product, quantity) in lines)
        {
            await client.PutAsJsonAsync($"/api/carts/{id}/lines/{product}", new { quantity }, Ct).Json();
        }

        return id;
    }

    public static async Task<HttpResponseMessage> CheckoutAsync(this HttpClient client, Guid cartId, string method, string phone, string? nuit = null, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/checkout", UriKind.Relative))
        {
            Content = JsonContent.Create(new { cartId, paymentMethod = method, phone, nuit }),
        };
        request.Headers.Add(IdempotencyHeaders.Key, key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request, Ct);
    }

    public static Task<JsonElement> TransitionAsync(this HttpClient backoffice, Guid orderId, string action, string? note = null) =>
        backoffice.PostAsJsonAsync($"/api/orders/{orderId}/transitions/{action}", new { note }, Ct).Json();

    public static async Task<int> StockAsync(this HttpClient client, Guid productId) =>
        (await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/catalog/products/{productId}", UriKind.Relative), Ct)).GetProperty("availableStock").GetInt32();

    public static async Task<JsonElement> Json(this Task<HttpResponseMessage> request, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await request;
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    /// <summary>Waits for something the outbox does asynchronously.</summary>
    public static async Task EventuallyAsync(Func<Task<bool>> condition, string what)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (!await condition())
        {
            try
            {
                await Task.Delay(100, timeout.Token);
            }
            catch (OperationCanceledException) when (!Ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting until {what}.");
            }
        }
    }
}
