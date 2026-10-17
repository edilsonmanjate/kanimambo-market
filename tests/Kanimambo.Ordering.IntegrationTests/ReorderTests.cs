using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Web.Idempotency;
using static Kanimambo.Ordering.IntegrationTests.Journey;

namespace Kanimambo.Ordering.IntegrationTests;

/// <summary>A restaurant repeats last week's order to the wholesaler, with changed quantities.</summary>
public sealed class ReorderTests(OrderingApiFactory factory)
{
    [Fact]
    public async Task A_b2b_customer_clones_last_weeks_order_adjusts_it_with_undo_and_submits_it()
    {
        var (_, products) = await factory.CreateSellerAsync(1_000m, ["Matola"], ("ARROZ", 900m, 100), ("OLEO", 300m, 100));
        var restaurant = factory.CreateClientAs($"restaurante-{Guid.NewGuid():N}", KnownRoles.B2B);
        var lastWeek = await restaurant.CheckoutAsync(
            await restaurant.CartWithAsync("Matola", (products["ARROZ"], 4), (products["OLEO"], 2)), "MPesa", NewPhone(), nuit: "400123456").Json();
        var lastWeekId = lastWeek.GetProperty("orderId").GetGuid();

        var draft = await restaurant.PostAsync(new Uri($"/api/orders/{lastWeekId}/reorder", UriKind.Relative), null, Ct).Json();
        var draftId = draft.GetProperty("id").GetGuid();
        Quantities(draft).ShouldBe([4, 2]);

        await restaurant.PutAsJsonAsync($"/api/order-drafts/{draftId}/lines/{products["ARROZ"]}", new { quantity = 6 }, Ct).Json();
        var mistake = await restaurant.PutAsJsonAsync($"/api/order-drafts/{draftId}/lines/{products["OLEO"]}", new { quantity = 0 }, Ct).Json();
        Quantities(mistake).ShouldBe([6]);

        var undone = await restaurant.PostAsync(new Uri($"/api/order-drafts/{draftId}/undo", UriKind.Relative), null, Ct).Json();
        Quantities(undone).ShouldBe([6, 2]);

        var submitted = await SubmitAsync(restaurant, draftId).Json();
        submitted.GetProperty("subtotal").GetDecimal().ShouldBe((6 * 900m) + (2 * 300m));

        var original = await restaurant.GetFromJsonAsync<JsonElement>(new Uri($"/api/orders/{lastWeekId}", UriKind.Relative), Ct);
        Quantities(original).ShouldBe([4, 2]);   // the prototype is untouched

        var again = await SubmitAsync(restaurant, draftId).Json();
        again.GetProperty("alreadyPlaced").GetBoolean().ShouldBeTrue();
        again.GetProperty("orderId").GetGuid().ShouldBe(submitted.GetProperty("orderId").GetGuid());
    }

    [Fact]
    public async Task Back_office_can_undo_edits_on_a_customers_draft()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Matola"], ("ARROZ", 900m, 100));
        var restaurant = factory.CreateClientAs($"restaurante-{Guid.NewGuid():N}", KnownRoles.B2B);
        var order = await restaurant.CheckoutAsync(await restaurant.CartWithAsync("Matola", (products["ARROZ"], 3)), "Card", NewPhone(), nuit: "400765432").Json();
        var draft = await restaurant.PostAsync(new Uri($"/api/orders/{order.GetProperty("orderId").GetGuid()}/reorder", UriKind.Relative), null, Ct).Json();
        var draftId = draft.GetProperty("id").GetGuid();
        await restaurant.PutAsJsonAsync($"/api/order-drafts/{draftId}/lines/{products["ARROZ"]}", new { quantity = 30 }, Ct).Json();

        var undone = await factory.Backoffice.PostAsync(new Uri($"/api/order-drafts/{draftId}/undo", UriKind.Relative), null, Ct).Json();

        Quantities(undone).ShouldBe([3]);
        (await factory.CreateClientAs("outra-empresa", KnownRoles.B2B).GetAsync(new Uri($"/api/order-drafts/{draftId}", UriKind.Relative), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Consumers_cannot_create_recurring_orders()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 500m, 10));
        var consumer = factory.CreateClientAs($"cliente-{Guid.NewGuid():N}");
        var order = await consumer.CheckoutAsync(await consumer.CartWithAsync("Maputo", (products["A"], 1)), "MPesa", NewPhone()).Json();

        var reorder = await consumer.PostAsync(new Uri($"/api/orders/{order.GetProperty("orderId").GetGuid()}/reorder", UriKind.Relative), null, Ct);

        (await reorder.ErrorCodeAsync()).ShouldBe("ordering.reorder_b2b_only");
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid draftId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/order-drafts/{draftId}/submit", UriKind.Relative))
        {
            Content = JsonContent.Create(new { paymentMethod = "MPesa", phone = NewPhone(), nuit = "400123456" }),
        };
        request.Headers.Add(IdempotencyHeaders.Key, Guid.NewGuid().ToString());
        return client.SendAsync(request, Ct);
    }

    private static List<int> Quantities(JsonElement orderOrDraft) =>
        orderOrDraft.GetProperty("lines").EnumerateArray().OrderBy(l => l.GetProperty("sku").GetString()).Select(l => l.GetProperty("quantity").GetInt32()).ToList();
}
