using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Web.Idempotency;
using static Kanimambo.Ordering.IntegrationTests.Journey;

namespace Kanimambo.Ordering.IntegrationTests;

public sealed class CheckoutTests(OrderingApiFactory factory)
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task Checkout_orchestrates_prices_promotion_shipping_and_payment_then_the_cart_is_closed()
    {
        var (_, products) = await factory.CreateSellerAsync(300m, ["Maputo", "Matola"], ("A", 1_000m, 20), ("B", 500m, 20));
        var cart = await _guest.CartWithAsync("Maputo", (products["A"], 2), (products["B"], 1));

        var order = await _guest.CheckoutAsync(cart, "MPesa", "258841112233").Json();

        order.GetProperty("status").GetString().ShouldBe("AwaitingPayment");
        order.GetProperty("subtotal").GetDecimal().ShouldBe(2_500m);
        order.GetProperty("promotionName").GetString().ShouldStartWith("M-Pesa em Maputo");
        order.GetProperty("discount").GetDecimal().ShouldBe(125m);
        order.GetProperty("shippingFee").GetDecimal().ShouldBe(150m);   // interim flat fee for Maputo
        order.GetProperty("total").GetDecimal().ShouldBe(2_525m);
        order.GetProperty("displayNumber").GetString().ShouldStartWith("KM-");
        order.GetProperty("nextStep").GetString().ShouldBe("Confirm the MPesa payment of 2525.00 MZN on 258841112233.");

        var orderId = order.GetProperty("orderId").GetGuid();
        await EventuallyAsync(async () =>
            (await _guest.GetFromJsonAsync<JsonElement>(new Uri($"/api/carts/{cart}", UriKind.Relative), Ct)).GetProperty("checkedOutOrderId").ToString() == orderId.ToString(),
            "the Cart module closed the cart (OrderPlaced via the outbox)");

        var change = await _guest.PutAsJsonAsync($"/api/carts/{cart}/lines/{products["A"]}", new { quantity = 1 }, Ct);
        change.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Retrying_a_checkout_never_creates_a_second_order()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 800m, 10));
        var cart = await _guest.CartWithAsync("Maputo", (products["A"], 1));
        var key = Guid.NewGuid().ToString();
        var phone = NewPhone();

        var first = await _guest.CheckoutAsync(cart, "EMola", phone, key: key);
        var sameKey = await _guest.CheckoutAsync(cart, "EMola", phone, key: key);
        var newKey = await _guest.CheckoutAsync(cart, "EMola", phone).Json();

        var original = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        sameKey.Headers.GetValues(IdempotencyHeaders.Replayed).ShouldBe(["true"]);
        (await sameKey.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("orderId").GetGuid().ShouldBe(original.GetProperty("orderId").GetGuid());

        newKey.GetProperty("orderId").GetGuid().ShouldBe(original.GetProperty("orderId").GetGuid());
        newKey.GetProperty("alreadyPlaced").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task The_acceptance_chain_rejects_orders_with_a_typed_error()
    {
        var (_, wholesaler) = await factory.CreateSellerAsync(5_000m, ["Matola"], ("ARROZ-50", 3_500m, 10));
        var (_, maputoOnly) = await factory.CreateSellerAsync(0m, ["Maputo"], ("PEIXE", 420m, 10));

        var belowMinimum = await _guest.CheckoutAsync(await _guest.CartWithAsync("Matola", (wholesaler["ARROZ-50"], 1)), "MPesa", NewPhone());
        var notDelivered = await _guest.CheckoutAsync(await _guest.CartWithAsync("Beira", (maputoOnly["PEIXE"], 1)), "MPesa", NewPhone());

        var cart = await _guest.CartWithAsync("Maputo", (maputoOnly["PEIXE"], 3));
        await factory.SetStockAsync(maputoOnly["PEIXE"], 2);
        var noStock = await _guest.CheckoutAsync(cart, "MPesa", NewPhone());

        belowMinimum.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await belowMinimum.ErrorCodeAsync()).ShouldBe("ordering.below_seller_minimum");
        (await notDelivered.ErrorCodeAsync()).ShouldBe("ordering.delivery_zone_not_covered");
        (await noStock.ErrorCodeAsync()).ShouldBe("ordering.insufficient_stock");
    }

    [Fact]
    public async Task Business_customers_must_give_a_nuit_and_phones_are_validated()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 500m, 10));
        var restaurant = factory.CreateClientAs($"restaurante-{Guid.NewGuid():N}", KnownRoles.B2B);

        var withoutNuit = await restaurant.CheckoutAsync(await restaurant.CartWithAsync("Maputo", (products["A"], 1)), "MKesh", NewPhone());
        var badPhone = await _guest.CheckoutAsync(await _guest.CartWithAsync("Maputo", (products["A"], 1)), "MKesh", "841234567");

        withoutNuit.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await withoutNuit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("nuit", out _).ShouldBeTrue();
        (await badPhone.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").GetProperty("phone")[0].GetString()
            .ShouldBe("Phone numbers use the 258XXXXXXXXX format.");
    }

    [Fact]
    public async Task One_phone_number_is_limited_to_five_orders_a_day()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 100m, 100));
        var phone = NewPhone();

        for (var i = 0; i < 5; i++)
        {
            await _guest.CheckoutAsync(await _guest.CartWithAsync("Maputo", (products["A"], 1)), "CashOnDelivery", phone).Json();
        }

        var sixth = await _guest.CheckoutAsync(await _guest.CartWithAsync("Maputo", (products["A"], 1)), "CashOnDelivery", phone);

        (await sixth.ErrorCodeAsync()).ShouldBe("ordering.fraud_limit_orders");
    }

    [Fact]
    public async Task Customers_can_cancel_their_own_orders_but_not_someone_elses()
    {
        var (_, products) = await factory.CreateSellerAsync(0m, ["Maputo"], ("A", 300m, 10));
        var ana = factory.CreateClientAs($"ana-{Guid.NewGuid():N}");
        var order = await ana.CheckoutAsync(await ana.CartWithAsync("Maputo", (products["A"], 1)), "Card", NewPhone()).Json();
        var orderId = order.GetProperty("orderId").GetGuid();

        (await factory.CreateClientAs("rui").PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { note = "x" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _guest.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { note = "x" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var cancelled = await ana.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { note = "Mudei de ideias" }, Ct).Json();

        cancelled.GetProperty("status").GetString().ShouldBe("Cancelled");
        cancelled.GetProperty("allowedActions").GetArrayLength().ShouldBe(0);
        cancelled.GetProperty("history").EnumerateArray().Last().GetProperty("reason").GetString().ShouldBe("Mudei de ideias");
    }
}
