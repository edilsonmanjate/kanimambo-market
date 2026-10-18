using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Web.Idempotency;

namespace Kanimambo.Payments.IntegrationTests;

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
