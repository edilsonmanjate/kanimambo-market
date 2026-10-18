using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kanimambo.Cart.IntegrationTests;

/// <summary>Small HTTP helpers so tests read like the customer's journey.</summary>
internal static class CartApi
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<Guid> ProductIdAsync(this HttpClient client, string name, string sku)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/catalog/products?q={Uri.EscapeDataString(name)}&pageSize=100", UriKind.Relative), Ct);
        return page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("sku").GetString() == sku).GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> OpenCartAsync(this HttpClient client, string city)
    {
        var response = await client.PostAsJsonAsync("/api/carts", new { deliveryCity = city }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    public static Task<HttpResponseMessage> SetLineAsync(this HttpClient client, Guid cartId, Guid productId, int quantity) =>
        client.PutAsJsonAsync($"/api/carts/{cartId}/lines/{productId}", new { quantity }, Ct);

    public static async Task<JsonElement> OkJsonAsync(this Task<HttpResponseMessage> request)
    {
        var response = await request;
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    public static Task<JsonElement> PricingAsync(this HttpClient client, Guid cartId, string? paymentMethod = null) =>
        client.GetAsync(new Uri($"/api/carts/{cartId}/pricing{(paymentMethod is null ? "" : $"?paymentMethod={paymentMethod}")}", UriKind.Relative), Ct).OkJsonAsync();

    public static Guid Id(this JsonElement json) => json.GetProperty("id").GetGuid();
}
