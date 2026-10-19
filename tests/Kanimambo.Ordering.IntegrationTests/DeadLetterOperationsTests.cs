using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Identity;
using static Kanimambo.Ordering.IntegrationTests.Journey;

namespace Kanimambo.Ordering.IntegrationTests;

/// <summary>Operations: a subscriber that kept failing left a dead letter; once fixed, it is replayed for that subscriber only.</summary>
public sealed class DeadLetterOperationsTests(OrderingApiFactory factory)
{
    private const string FlakySubscriber = "Kanimambo.Ordering.IntegrationTests.FlakyOrderPaidSubscriber";

    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task A_dead_letter_is_listed_and_replayed_for_the_subscriber_that_missed_it()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 500m, 10));
        var order = (await _guest.CheckoutAsync(await _guest.CartWithAsync("Maputo", (products["A"], 1)), "Card", NewPhone()).Json()).GetProperty("orderId").GetGuid();
        factory.Failures.FailAlways[order] = true;
        await factory.Backoffice.TransitionAsync(order, "ConfirmPayment", "CARD-DL");

        JsonElement letter = default;
        await EventuallyAsync(async () => (letter = (await DeadLettersAsync()).FirstOrDefault(l => l.GetProperty("payload").GetString()!.Contains(order.ToString(), StringComparison.Ordinal))).ValueKind != JsonValueKind.Undefined,
            "the OrderPaid message was dead-lettered");
        letter.GetProperty("module").GetString().ShouldBe("Ordering");
        letter.GetProperty("eventType").GetString().ShouldBe("OrderPaidIntegrationEvent");
        letter.GetProperty("lastError").GetString().ShouldNotBeNull().ShouldContain("Simulated outage");
        letter.GetProperty("deliveredTo").EnumerateArray().Select(s => s.GetString()).ShouldNotContain(FlakySubscriber);
        var callsBefore = factory.Failures.Calls[order];

        factory.Failures.FailAlways.TryRemove(order, out _);
        var replay = await factory.Backoffice.PostAsync(new Uri($"/api/operations/outbox/Ordering/dead-letters/{letter.GetProperty("id").GetGuid()}/replay", UriKind.Relative), null, Ct);

        replay.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await EventuallyAsync(async () => factory.Failures.Calls[order] > callsBefore && !(await DeadLettersAsync()).Any(l => l.GetProperty("id").GetGuid() == letter.GetProperty("id").GetGuid()),
            "the replay reached the fixed subscriber");
        (await _guest.StockAsync(products["A"])).ShouldBe(9); // the stock subscriber had succeeded before and was not called again
    }

    [Fact]
    public async Task The_overview_lists_every_publishing_module()
    {
        var overview = await factory.Backoffice.GetFromJsonAsync<JsonElement[]>(new Uri("/api/operations/outbox", UriKind.Relative), Ct);

        overview.ShouldNotBeNull().Select(o => o.GetProperty("module").GetString())
            .ShouldBe(["Invoicing", "Ordering", "Payments", "Shipping"]);
    }

    [Fact]
    public async Task Replaying_something_that_is_not_a_dead_letter_is_not_found_and_customers_cannot_see_operations()
    {
        var unknown = await factory.Backoffice.PostAsync(new Uri($"/api/operations/outbox/Ordering/dead-letters/{Guid.NewGuid()}/replay", UriKind.Relative), null, Ct);
        var badModule = await factory.Backoffice.GetAsync(new Uri("/api/operations/outbox/dead-letters?module=Nope", UriKind.Relative), Ct);
        var customer = await factory.CreateClientAs("cliente", KnownRoles.B2B).GetAsync(new Uri("/api/operations/outbox", UriKind.Relative), Ct);

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await badModule.Content.ReadAsStringAsync(Ct)).ShouldContain("Invoicing, Ordering, Payments, Shipping");
        customer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<JsonElement[]> DeadLettersAsync() =>
        (await factory.Backoffice.GetFromJsonAsync<JsonElement[]>(new Uri("/api/operations/outbox/dead-letters?module=Ordering", UriKind.Relative), Ct))!;
}
