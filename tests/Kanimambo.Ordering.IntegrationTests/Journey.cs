using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Web.Idempotency;

namespace Kanimambo.Ordering.IntegrationTests;

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
