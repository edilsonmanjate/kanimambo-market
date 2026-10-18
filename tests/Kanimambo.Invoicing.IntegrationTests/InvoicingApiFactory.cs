using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Invoicing.IntegrationTests;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Web.Idempotency;
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

internal static class Journey
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string NewNuit() => $"5{Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture)}";

    public static string Phone() => $"25884{Random.Shared.Next(100, 999)}{Random.Shared.Next(1_000, 9_999)}";

    public static async Task<JsonElement> RegisterTaxpayerAsync(this HttpClient backoffice, Guid sellerId, string regime, string? nuit = null) =>
        await backoffice.PutAsJsonAsync($"/api/invoicing/taxpayers/{sellerId}",
            new { nuit = nuit ?? NewNuit(), legalName = $"Vendedor {sellerId:N}"[..20], address = "Maputo", regime }, Ct).Json();

    /// <summary>Cart → checkout (same-day in Maputo) → back-office confirms the payment → OrderPaid.</summary>
    public static async Task<JsonElement> PaidOrderAsync(this HttpClient client, HttpClient backoffice, string paymentMethod, string? nuit, params (Guid Product, int Quantity)[] lines)
    {
        var cart = await client.PostAsJsonAsync("/api/carts", new { deliveryCity = "Maputo" }, Ct).Json(HttpStatusCode.Created);
        var cartId = cart.GetProperty("id").GetGuid();
        foreach (var (product, quantity) in lines)
        {
            await client.PutAsJsonAsync($"/api/carts/{cartId}/lines/{product}", new { quantity }, Ct).Json();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/checkout", UriKind.Relative))
        {
            Content = JsonContent.Create(new { cartId, paymentMethod, phone = Phone(), nuit, deliveryMethod = "SameDay" }),
        };
        request.Headers.Add(IdempotencyHeaders.Key, Guid.NewGuid().ToString());
        var order = await client.SendAsync(request, Ct).Json();
        await backoffice.PostAsJsonAsync($"/api/orders/{order.GetProperty("orderId").GetGuid()}/transitions/ConfirmPayment", new { note = "MP-TEST" }, Ct).Json();
        return order;
    }

    public static async Task<JsonElement[]> DocumentsAsync(this HttpClient backoffice, Guid orderId, int expected)
    {
        JsonElement[] documents = [];
        await EventuallyAsync(async () =>
        {
            documents = (await backoffice.GetFromJsonAsync<JsonElement[]>(new Uri($"/api/invoicing/documents?orderId={orderId}", UriKind.Relative), Ct))!;
            return documents.Length >= expected;
        }, $"order {orderId} has {expected} fiscal documents");
        return documents;
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
