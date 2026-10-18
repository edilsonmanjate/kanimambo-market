using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Web.Idempotency;

namespace Kanimambo.Invoicing.IntegrationTests;

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
