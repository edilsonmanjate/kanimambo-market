using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kanimambo.SharedKernel.Infrastructure.Webhooks;
using Kanimambo.Simulators.Courier;
using Microsoft.Extensions.DependencyInjection;
using static Kanimambo.Shipping.IntegrationTests.Journey;

namespace Kanimambo.Shipping.IntegrationTests;

/// <summary>
/// After dispatch: Ordering → (outbox) → Shipping books a courier per parcel through the courier's adapter → the
/// simulated courier sends signed tracking webhooks → when every parcel is delivered, Shipping tells Ordering (outbox).
/// </summary>
public sealed class DeliveryTests(ShippingApiFactory factory)
{
    private readonly HttpClient _customer = factory.CreateClientAs($"cliente-{Guid.NewGuid():N}");

    [Fact]
    public async Task A_paid_same_day_order_is_delivered_by_motorbike_and_marked_delivered()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo"], 3m);
        var cart = await _customer.CartAsync("Maputo", (product, 1));
        var orderId = (await _customer.CheckoutAsync(cart, "MPesa", Phone(), "SameDay", "KaMpfumo").Json()).GetProperty("orderId").GetGuid();
        await _customer.OrderReachesAsync(orderId, "Paid");

        await factory.Backoffice.DispatchAsync(orderId);

        var delivered = await _customer.OrderReachesAsync(orderId, "Delivered");
        delivered.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("to").GetString())
            .ShouldBe(["AwaitingPayment", "Paid", "Preparing", "OutForDelivery", "Delivered"]);
        var parcel = (await factory.Backoffice.ConsignmentReachesAsync(orderId, "Delivered")).GetProperty("parcels").EnumerateArray().ShouldHaveSingleItem();
        parcel.GetProperty("courier").GetString().ShouldBe("Txova");
        parcel.GetProperty("vehicle").GetString().ShouldBe("Motorbike");
        parcel.GetProperty("trackingCode").GetString().ShouldStartWith("TX-");
        TrackingOf(parcel).ShouldBe(["Requested", "Booked", "PickedUp", "Delivered"]);
    }

    [Fact]
    public async Task A_cash_order_to_Beira_travels_by_cargo_and_is_paid_when_delivered()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Beira"], 2m);
        var cart = await _customer.CartAsync("Beira", (product, 3));
        var placed = await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone()).Json();
        var orderId = placed.GetProperty("orderId").GetGuid();

        await factory.Backoffice.DispatchAsync(orderId);

        var delivered = await _customer.OrderReachesAsync(orderId, "Delivered");
        delivered.GetProperty("paymentReference").GetString().ShouldStartWith("COD-KM-");
        var parcel = (await factory.Backoffice.ConsignmentReachesAsync(orderId, "Delivered")).GetProperty("parcels")[0];
        parcel.GetProperty("courier").GetString().ShouldBe("Mbila");
        parcel.GetProperty("cashToCollect").GetDecimal().ShouldBe(placed.GetProperty("total").GetDecimal());
        TrackingOf(parcel).ShouldBe(["Requested", "Booked", "PickedUp", "InTransit", "Delivered"]);
    }

    [Fact]
    public async Task An_order_from_two_sellers_is_delivered_when_both_parcels_are()
    {
        var (sellerA, productA) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo"], 2m);
        var (sellerB, productB) = await factory.CreateSellerWithProductAsync("Matola", ["Maputo"], 2m);
        var cart = await _customer.CartAsync("Maputo", (productA, 1), (productB, 1));
        var placed = await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone(), "SameDay").Json();
        placed.GetProperty("shippingFee").GetDecimal().ShouldBe(300m); // one zone-A delivery per seller
        var orderId = placed.GetProperty("orderId").GetGuid();

        await factory.Backoffice.DispatchAsync(orderId);

        await _customer.OrderReachesAsync(orderId, "Delivered");
        var parcels = (await factory.Backoffice.ConsignmentReachesAsync(orderId, "Delivered")).GetProperty("parcels").EnumerateArray().ToList();
        parcels.Select(p => p.GetProperty("sellerId").GetGuid()).ShouldBe([sellerA, sellerB], ignoreOrder: true);
        parcels.Select(p => p.GetProperty("status").GetString()).ShouldAllBe(s => s == "Delivered");
    }

    [Fact]
    public async Task A_pickup_order_waits_at_the_point_until_the_customer_collects_it()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo"], 2m);
        var cart = await _customer.CartAsync("Maputo", (product, 1));
        var placed = await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone(), "Pickup", pickupPointId: "maputo-baixa").Json();
        placed.GetProperty("shippingFee").GetDecimal().ShouldBe(0m);
        var orderId = placed.GetProperty("orderId").GetGuid();

        await factory.Backoffice.DispatchAsync(orderId);

        var ready = await factory.Backoffice.ConsignmentReachesAsync(orderId, "ReadyForCollection");
        (await _customer.OrderAsync(orderId)).GetProperty("status").GetString().ShouldBe("OutForDelivery");

        await factory.Backoffice.PostAsync(new Uri($"/api/shipping/consignments/{ready.GetProperty("id").GetGuid()}/collected", UriKind.Relative), null, Ct).Json();

        await _customer.OrderReachesAsync(orderId, "Delivered");
    }

    [Fact]
    public async Task A_failed_delivery_attempt_fails_the_parcel_and_leaves_the_order_out_for_delivery()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo"], 2m);
        var cart = await _customer.CartAsync("Maputo", (product, 1));
        var orderId = (await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone("0101")).Json()).GetProperty("orderId").GetGuid();

        await factory.Backoffice.DispatchAsync(orderId);

        var failed = await factory.Backoffice.ConsignmentReachesAsync(orderId, "Failed");
        failed.GetProperty("parcels")[0].GetProperty("failureReason").GetString().ShouldBe("Txova: RECIPIENT_ABSENT");
        (await _customer.OrderAsync(orderId)).GetProperty("status").GetString().ShouldBe("OutForDelivery");
    }

    [Fact]
    public async Task A_booking_the_courier_refuses_fails_the_parcel_with_the_couriers_reason()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Beira"], 2m);
        var cart = await _customer.CartAsync("Beira", (product, 1));
        var orderId = (await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone("0102")).Json()).GetProperty("orderId").GetGuid();

        await factory.Backoffice.DispatchAsync(orderId);

        var parcel = (await factory.Backoffice.ConsignmentReachesAsync(orderId, "Failed")).GetProperty("parcels")[0];
        parcel.GetProperty("failureReason").GetString().ShouldStartWith("Mbila: Destino não servido");
        TrackingOf(parcel).ShouldBe(["Requested", "Failed"]);
    }

    [Fact]
    public async Task A_courier_outage_at_booking_is_retried_and_the_same_booking_is_kept()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo"], 2m);
        var cart = await _customer.CartAsync("Maputo", (product, 1));
        var orderId = (await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone("0103")).Json()).GetProperty("orderId").GetGuid();

        await factory.Backoffice.DispatchAsync(orderId);

        await _customer.OrderReachesAsync(orderId, "Delivered");
        var parcel = (await factory.Backoffice.ConsignmentReachesAsync(orderId, "Delivered")).GetProperty("parcels")[0];
        var waybills = factory.Courier.Services.GetRequiredService<Waybills>();
        waybills.RequestsFor(parcel.GetProperty("reference").GetString()!).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Theory]
    [InlineData("txova", null)]
    [InlineData("mbila", "sha256=0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Webhooks_without_a_valid_signature_are_rejected(string courier, string? signature)
    {
        const string body = """{"reference":"KM-1-1","referenciaCliente":"KM-1-1","event":"DELIVERED","estado":"ENTREGUE","deliveryId":"x","numeroGuia":"x"}""";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/shipping/webhooks/{courier}", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (signature is not null)
        {
            request.Headers.Add(WebhookSignature.Header, signature);
        }

        (await factory.CreateClient().SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_repeated_webhook_is_acknowledged_and_changes_nothing()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo"], 2m);
        var cart = await _customer.CartAsync("Maputo", (product, 1));
        var orderId = (await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone()).Json()).GetProperty("orderId").GetGuid();
        await factory.Backoffice.DispatchAsync(orderId);
        var parcel = (await factory.Backoffice.ConsignmentReachesAsync(orderId, "Delivered")).GetProperty("parcels")[0];

        var body = JsonSerializer.Serialize(new { deliveryId = parcel.GetProperty("trackingCode").GetString(), reference = parcel.GetProperty("reference").GetString(), @event = "PICKED_UP" });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/shipping/webhooks/txova", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WebhookSignature.Header, WebhookSignature.Sign(body, "dev-txova-webhook-secret"));

        (await factory.CreateClient().SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var after = await factory.Backoffice.GetFromJsonAsync<JsonElement>(new Uri($"/api/shipping/consignments?orderId={orderId}", UriKind.Relative), Ct);
        after.GetProperty("status").GetString().ShouldBe("Delivered");
        TrackingOf(after.GetProperty("parcels")[0]).Count().ShouldBe(TrackingOf(parcel).Count());
    }
}
