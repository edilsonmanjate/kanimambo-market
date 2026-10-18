using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Kanimambo.Shipping.IntegrationTests.Journey;

namespace Kanimambo.Shipping.IntegrationTests;

/// <summary>Quoting at checkout (Strategy) — prices come from the Shipping configuration in appsettings.json.</summary>
public sealed class ShippingQuoteTests(ShippingApiFactory factory)
{
    private readonly HttpClient _customer = factory.CreateClientAs($"cliente-{Guid.NewGuid():N}");

    [Fact]
    public async Task A_cart_gets_every_delivery_option_cheapest_first()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo", "Beira"], 2m);
        var cart = await _customer.CartAsync("Maputo", (product, 2));

        var options = await _customer.GetFromJsonAsync<JsonElement[]>(new Uri($"/api/shipping/options?cartId={cart}&district=KaMavota", UriKind.Relative), Ct);

        options.ShouldNotBeNull().Select(o => (o.GetProperty("method").GetString(), o.GetProperty("fee").GetDecimal()))
            .ShouldBe([("Pickup", 0m), ("SameDay", 220m), ("Courier", 300m)]);
        options[1].GetProperty("parcels")[0].GetProperty("vehicle").GetString().ShouldBe("Motorbike");
    }

    [Fact]
    public async Task Courier_delivery_is_priced_by_destination_and_weight()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo", "Beira"], 6m);
        var cart = await _customer.CartAsync("Beira", (product, 2));

        var order = await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone()).Json();

        // 12 kg to Beira: 450 base + (12 − 5 included) × 25 per kg. Same-day does not reach Beira, so the courier is chosen.
        order.GetProperty("shippingFee").GetDecimal().ShouldBe(625m);
        order.GetProperty("deliveryMethod").GetString().ShouldBe("Courier");
        order.GetProperty("estimatedDelivery").GetString().ShouldBe(MaputoToday.AddDays(3).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_heavy_same_day_order_goes_by_txopela()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Matola", ["Maputo"], 25m);
        var cart = await _customer.CartAsync("Maputo", (product, 1));

        var order = await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone(), "SameDay", "KaMpfumo").Json();

        order.GetProperty("shippingFee").GetDecimal().ShouldBe(350m); // zone A 150 + txopela 200
    }

    [Fact]
    public async Task An_unknown_delivery_method_is_a_validation_error()
    {
        var (_, product) = await factory.CreateSellerWithProductAsync("Maputo", ["Maputo"], 1m);
        var cart = await _customer.CartAsync("Maputo", (product, 1));

        var response = await _customer.CheckoutAsync(cart, "CashOnDelivery", Phone(), "Drone");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("SameDay, Courier, Pickup");
    }

    [Fact]
    public async Task Pickup_points_are_listed_per_city()
    {
        var points = await _customer.GetFromJsonAsync<JsonElement[]>(new Uri("/api/shipping/pickup-points?city=Maputo", UriKind.Relative), Ct);

        points!.Select(p => p.GetProperty("id").GetString()).ShouldBe(["maputo-baixa"]);
    }
}
