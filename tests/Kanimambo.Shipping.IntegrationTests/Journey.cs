using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Web.Idempotency;

namespace Kanimambo.Shipping.IntegrationTests;

internal static class Journey
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Today in Maputo, which is what delivery dates are based on.</summary>
    public static DateOnly MaputoToday => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).DateTime);

    /// <summary>
    /// A fresh M-Pesa number (fresh, so anti-fraud limits never interfere) ending in the courier scenario suffix:
    /// 0101 recipient absent, 0102 booking refused, 0103 courier outage; otherwise everything succeeds.
    /// </summary>
    public static string Phone(string suffix = "") =>
        $"25884{Random.Shared.Next(100, 999)}{(suffix.Length > 0 ? suffix : Random.Shared.Next(1_000, 9_999).ToString(CultureInfo.InvariantCulture))}";

    public static async Task<Guid> CartAsync(this HttpClient client, string city, params (Guid Product, int Quantity)[] lines)
    {
        var cart = await client.PostAsJsonAsync("/api/carts", new { deliveryCity = city }, Ct).Json(HttpStatusCode.Created);
        var id = cart.GetProperty("id").GetGuid();
        foreach (var (product, quantity) in lines)
        {
            await client.PutAsJsonAsync($"/api/carts/{id}/lines/{product}", new { quantity }, Ct).Json();
        }

        return id;
    }

    public static async Task<HttpResponseMessage> CheckoutAsync(this HttpClient client, Guid cartId, string paymentMethod, string phone,
        string? deliveryMethod = null, string? district = null, string? pickupPointId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/checkout", UriKind.Relative))
        {
            Content = JsonContent.Create(new { cartId, paymentMethod, phone, deliveryMethod, deliveryDistrict = district, pickupPointId }),
        };
        request.Headers.Add(IdempotencyHeaders.Key, Guid.NewGuid().ToString());
        return await client.SendAsync(request, Ct);
    }

    public static Task<JsonElement> OrderAsync(this HttpClient client, Guid orderId) =>
        client.GetFromJsonAsync<JsonElement>(new Uri($"/api/orders/{orderId}", UriKind.Relative), Ct);

    public static Task<JsonElement> TransitionAsync(this HttpClient backoffice, Guid orderId, string action) =>
        backoffice.PostAsJsonAsync($"/api/orders/{orderId}/transitions/{action}", new { note = (string?)null }, Ct).Json();

    /// <summary>Back-office prepares and dispatches the order; Shipping then books the couriers.</summary>
    public static async Task DispatchAsync(this HttpClient backoffice, Guid orderId)
    {
        await backoffice.TransitionAsync(orderId, "StartPreparing");
        await backoffice.TransitionAsync(orderId, "Dispatch");
    }

    public static async Task<JsonElement> OrderReachesAsync(this HttpClient client, Guid orderId, string status)
    {
        JsonElement order = default;
        await EventuallyAsync(async () => (order = await client.OrderAsync(orderId)).GetProperty("status").GetString() == status, $"order {orderId} is {status}");
        return order;
    }

    /// <summary>Waits until the order's consignment reaches the status.</summary>
    public static async Task<JsonElement> ConsignmentReachesAsync(this HttpClient backoffice, Guid orderId, string status)
    {
        JsonElement consignment = default;
        await EventuallyAsync(async () =>
        {
            var response = await backoffice.GetAsync(new Uri($"/api/shipping/consignments?orderId={orderId}", UriKind.Relative), Ct);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            consignment = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            return consignment.GetProperty("status").GetString() == status;
        }, $"the consignment of order {orderId} is {status}");
        return consignment;
    }

    public static IEnumerable<string?> TrackingOf(JsonElement parcel) =>
        parcel.GetProperty("tracking").EnumerateArray().Select(t => t.GetProperty("status").GetString());

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
