using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Notifications.IntegrationTests;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Web.Idempotency;
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

internal static class Journey
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A fresh number; <paramref name="suffix"/> 0201 = SMS outage, 0202 = not on WhatsApp.</summary>
    public static string Phone(string suffix = "") =>
        $"25884{Random.Shared.Next(100, 999)}{(suffix.Length > 0 ? suffix : Random.Shared.Next(1_000, 9_999).ToString(CultureInfo.InvariantCulture))}";

    public static async Task<JsonElement> PlaceAsync(this HttpClient client, Guid product, string paymentMethod, string phone)
    {
        var cart = await client.PostAsJsonAsync("/api/carts", new { deliveryCity = "Maputo" }, Ct).Json(HttpStatusCode.Created);
        var cartId = cart.GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/carts/{cartId}/lines/{product}", new { quantity = 1 }, Ct).Json();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/checkout", UriKind.Relative))
        {
            Content = JsonContent.Create(new { cartId, paymentMethod, phone, deliveryMethod = "SameDay" }),
        };
        request.Headers.Add(IdempotencyHeaders.Key, Guid.NewGuid().ToString());
        return await client.SendAsync(request, Ct).Json();
    }

    public static Task<JsonElement> TransitionAsync(this HttpClient backoffice, Guid orderId, string action, string? note = null) =>
        backoffice.PostAsJsonAsync($"/api/orders/{orderId}/transitions/{action}", new { note }, Ct).Json();

    public static async Task<JsonElement> Json(this Task<HttpResponseMessage> request, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await request;
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> done, string what)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            var value = await read();
            if (done(value))
            {
                return value;
            }

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
