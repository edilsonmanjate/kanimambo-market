using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Payments.IntegrationTests;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Web.Idempotency;
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

/// <summary>The customer journey over HTTP, and the phone numbers that pick the simulator's scenarios.</summary>
internal static class Journey
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// A fresh number for the operator prefix (fresh, so the anti-fraud limits per phone never interfere), ending in the
    /// scenario suffix: 0001 insufficient balance, 0002 duplicate, 0003 no answer, 0004 delayed callback, else success.
    /// </summary>
    public static string Phone(string prefix, string suffix = "") =>
        $"258{prefix}{Random.Shared.Next(100, 999)}{(suffix.Length > 0 ? suffix : Random.Shared.Next(1_000, 9_999).ToString(System.Globalization.CultureInfo.InvariantCulture))}";

    public static async Task<Guid> PlaceOrderAsync(this HttpClient client, Guid product, string method, string phone)
    {
        var cart = await client.PostAsJsonAsync("/api/carts", new { deliveryCity = "Maputo" }, Ct).Json(HttpStatusCode.Created);
        var cartId = cart.GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/carts/{cartId}/lines/{product}", new { quantity = 2 }, Ct).Json();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/checkout", UriKind.Relative))
        {
            Content = JsonContent.Create(new { cartId, paymentMethod = method, phone }),
        };
        request.Headers.Add(IdempotencyHeaders.Key, Guid.NewGuid().ToString());
        var order = await client.SendAsync(request, Ct).Json();
        return order.GetProperty("orderId").GetGuid();
    }

    public static Task<JsonElement> OrderAsync(this HttpClient client, Guid orderId) =>
        client.GetFromJsonAsync<JsonElement>(new Uri($"/api/orders/{orderId}", UriKind.Relative), Ct);

    public static async Task<JsonElement[]> PaymentsAsync(this HttpClient backoffice, Guid orderId) =>
        [.. (await backoffice.GetFromJsonAsync<JsonElement>(new Uri($"/api/payments?orderId={orderId}", UriKind.Relative), Ct)).EnumerateArray()];

    public static async Task<int> StockAsync(this HttpClient client, Guid productId) =>
        (await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/catalog/products/{productId}", UriKind.Relative), Ct)).GetProperty("availableStock").GetInt32();

    /// <summary>Waits until the order reaches the status (payments settle asynchronously).</summary>
    public static async Task<JsonElement> OrderReachesAsync(this HttpClient client, Guid orderId, string status)
    {
        JsonElement order = default;
        await EventuallyAsync(async () => (order = await client.OrderAsync(orderId)).GetProperty("status").GetString() == status,
            $"order {orderId} is {status}");
        return order;
    }

    public static async Task<JsonElement> Json(this Task<HttpResponseMessage> request, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await request;
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    public static async Task EventuallyAsync(Func<Task<bool>> condition, string what)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
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
