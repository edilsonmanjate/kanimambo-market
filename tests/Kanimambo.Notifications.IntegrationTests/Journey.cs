using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Web.Idempotency;

namespace Kanimambo.Notifications.IntegrationTests;

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
