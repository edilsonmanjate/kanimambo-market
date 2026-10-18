using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Ordering.Infrastructure;
using Kanimambo.SharedKernel.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Kanimambo.Ordering.IntegrationTests.Journey;

namespace Kanimambo.Ordering.IntegrationTests;

public sealed class LifecycleAndOutboxTests(OrderingApiFactory factory)
{
    private const string StockSubscriber = "Kanimambo.Catalog.Application.Stock.ReserveStockWhenOrderIsPaid";
    private const string FlakySubscriber = "Kanimambo.Ordering.IntegrationTests.FlakyOrderPaidSubscriber";
    private const string InvoicingSubscriber = "Kanimambo.Invoicing.Application.InvoiceWhenOrderPaid";

    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task Confirming_a_payment_reserves_stock_through_the_outbox()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 1_000m, 20), ("B", 500m, 20));
        var order = await PlaceAsync("MPesa", (products["A"], 2), (products["B"], 1));

        var paid = await factory.Backoffice.TransitionAsync(order, "ConfirmPayment", "MP-1234567");

        paid.GetProperty("status").GetString().ShouldBe("Paid");
        paid.GetProperty("paymentReference").GetString().ShouldBe("MP-1234567");
        await EventuallyAsync(async () => await _guest.StockAsync(products["A"]) == 18 && await _guest.StockAsync(products["B"]) == 19,
            "Catalog reserved the stock (OrderPaid via the outbox)");
    }

    [Fact]
    public async Task Cash_on_delivery_orders_are_paid_and_reserve_stock_only_when_delivered()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 400m, 10));
        var order = await PlaceAsync("CashOnDelivery", (products["A"], 3));
        var backoffice = factory.Backoffice;

        var pending = await _guest.GetFromJsonAsync<JsonElement>(new Uri($"/api/orders/{order}", UriKind.Relative), Ct);
        pending.GetProperty("status").GetString().ShouldBe("Pending");
        pending.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["StartPreparing", "Cancel"]);

        await backoffice.TransitionAsync(order, "StartPreparing");
        await backoffice.TransitionAsync(order, "Dispatch");
        await Task.Delay(500, Ct); // give the outbox time: nothing to reserve yet
        (await _guest.StockAsync(products["A"])).ShouldBe(10);

        var delivered = await backoffice.TransitionAsync(order, "Deliver");

        delivered.GetProperty("paymentReference").GetString().ShouldStartWith("COD-KM-");
        await EventuallyAsync(async () => await _guest.StockAsync(products["A"]) == 7, "stock was reserved on delivery");
        delivered.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("to").GetString())
            .ShouldBe(["Preparing", "OutForDelivery", "Delivered"]);
    }

    [Fact]
    public async Task Invalid_transitions_are_refused_with_a_conflict()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 400m, 10));
        var order = await PlaceAsync("MPesa", (products["A"], 1));

        var dispatchUnpaid = await factory.Backoffice.PostAsJsonAsync($"/api/orders/{order}/transitions/Dispatch", new { note = (string?)null }, Ct);
        var unknown = await factory.Backoffice.PostAsJsonAsync($"/api/orders/{order}/transitions/Teleport", new { note = (string?)null }, Ct);

        dispatchUnpaid.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await dispatchUnpaid.ErrorCodeAsync()).ShouldBe("ordering.invalid_transition");
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_failing_subscriber_is_retried_alone_while_the_others_receive_the_event()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 600m, 10));
        var order = await PlaceAsync("MKesh", (products["A"], 2));
        factory.Failures.FailTimes[order] = 2;

        await factory.Backoffice.TransitionAsync(order, "ConfirmPayment", "MK-998877");

        await EventuallyAsync(async () => await _guest.StockAsync(products["A"]) == 8, "stock was reserved despite the failing subscriber");
        var message = await EventuallyProcessedAsync(order);

        message.Attempts.ShouldBe(2);
        message.DeliveredTo.ShouldBe([StockSubscriber, FlakySubscriber, InvoicingSubscriber], ignoreOrder: true);
        factory.Failures.Calls[order].ShouldBe(3);
        (await _guest.StockAsync(products["A"])).ShouldBe(8); // reserved exactly once
    }

    [Fact]
    public async Task A_subscriber_that_never_recovers_is_dead_lettered_after_the_maximum_attempts()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 600m, 10));
        var order = await PlaceAsync("Card", (products["A"], 1));
        factory.Failures.FailAlways[order] = true;

        await factory.Backoffice.TransitionAsync(order, "ConfirmPayment", "CARD-1");

        OutboxMessage? message = null;
        await EventuallyAsync(async () => (message = await OrderPaidMessageAsync(order)) is { DeadLettered: true }, "the message was dead-lettered");

        message!.Attempts.ShouldBe(3);
        message.ProcessedAt.ShouldBeNull();
        message.DeliveredTo.ShouldBe([StockSubscriber, InvoicingSubscriber], ignoreOrder: true);
        message.LastError!.ShouldContain("Simulated outage");
        (await _guest.StockAsync(products["A"])).ShouldBe(9);
    }

    [Fact]
    public async Task Paying_for_a_bundle_reserves_its_components()
    {
        var (seller, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("ARROZ", 400m, 20), ("OLEO", 200m, 20));
        var bundle = await factory.Backoffice.PostAsJsonAsync($"/api/catalog/sellers/{seller}/bundles", new
        {
            sku = $"CABAZ-{Guid.NewGuid():N}"[..20],
            name = "Cabaz de teste",
            category = "cabazes",
            components = new[] { new { productId = products["ARROZ"], quantity = 2 }, new { productId = products["OLEO"], quantity = 1 } },
        }, Ct).Json(HttpStatusCode.Created);

        var order = await PlaceAsync("MPesa", (bundle.GetGuid(), 3));
        await factory.Backoffice.TransitionAsync(order, "ConfirmPayment", "MP-CABAZ");

        await EventuallyAsync(async () => await _guest.StockAsync(products["ARROZ"]) == 14 && await _guest.StockAsync(products["OLEO"]) == 17,
            "the bundle's leaf products were reserved (Composite)");
    }

    private async Task<Guid> PlaceAsync(string method, params (Guid Product, int Quantity)[] lines)
    {
        var order = await _guest.CheckoutAsync(await _guest.CartWithAsync("Maputo", lines), method, NewPhone()).Json();
        return order.GetProperty("orderId").GetGuid();
    }

    private async Task<OutboxMessage> EventuallyProcessedAsync(Guid order)
    {
        OutboxMessage? message = null;
        await EventuallyAsync(async () => (message = await OrderPaidMessageAsync(order)) is { ProcessedAt: not null }, "the OrderPaid message was fully delivered");
        return message!;
    }

    private async Task<OutboxMessage?> OrderPaidMessageAsync(Guid order)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var paid = await db.Outbox.AsNoTracking()
            .Where(m => m.Type.StartsWith("Kanimambo.Ordering.Contracts.OrderPaidIntegrationEvent"))
            .ToListAsync(Ct);
        return paid.SingleOrDefault(m => m.Payload.Contains(order.ToString(), StringComparison.Ordinal));
    }
}
