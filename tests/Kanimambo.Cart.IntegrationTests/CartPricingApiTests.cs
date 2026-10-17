using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Identity;
using static Kanimambo.Cart.IntegrationTests.CartApi;

namespace Kanimambo.Cart.IntegrationTests;

public sealed class CartPricingApiTests(CartApiFactory factory)
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task The_mpesa_promotion_applies_only_when_paying_with_mpesa()
    {
        var cart = (await _guest.OpenCartAsync("Maputo")).Id();
        await _guest.SetLineAsync(cart, await _guest.ProductIdAsync("Arroz agulha", "MX-ARROZ-25"), 1).OkJsonAsync();
        await _guest.SetLineAsync(cart, await _guest.ProductIdAsync("Óleo de cozinha", "MX-OLEO-5"), 1).OkJsonAsync();

        var browsing = await _guest.PricingAsync(cart);
        var withMPesa = await _guest.PricingAsync(cart, "MPesa");

        browsing.GetProperty("subtotal").GetDecimal().ShouldBe(2_570m);
        browsing.GetProperty("promotion").GetProperty("name").GetString().ShouldBeEmpty();   // NoPromotion
        browsing.GetProperty("promotion").GetProperty("discount").GetDecimal().ShouldBe(0m);
        browsing.GetProperty("total").GetDecimal().ShouldBe(2_570m);
        browsing.GetProperty("customerType").GetString().ShouldBe("guest");

        withMPesa.GetProperty("promotion").GetProperty("name").GetString().ShouldStartWith("M-Pesa em Maputo");
        withMPesa.GetProperty("promotion").GetProperty("discount").GetDecimal().ShouldBe(128.50m);
        withMPesa.GetProperty("total").GetDecimal().ShouldBe(2_441.50m);
    }

    [Fact]
    public async Task The_best_promotion_wins_when_several_rules_match()
    {
        var cart = (await _guest.OpenCartAsync("Maputo")).Id();
        await _guest.SetLineAsync(cart, CatalogSeeder.CabazDoMes.Value, 1).OkJsonAsync();

        var withoutMPesa = await _guest.PricingAsync(cart);
        var withMPesa = await _guest.PricingAsync(cart, "MPesa");

        // Only "cabazes" applies: 150 MZN off 3 665.
        withoutMPesa.GetProperty("promotion").GetProperty("discount").GetDecimal().ShouldBe(150m);
        withoutMPesa.GetProperty("total").GetDecimal().ShouldBe(3_515m);

        // M-Pesa 5 % (183.25) beats the fixed 150.
        withMPesa.GetProperty("promotion").GetProperty("discount").GetDecimal().ShouldBe(183.25m);
        withMPesa.GetProperty("total").GetDecimal().ShouldBe(3_481.75m);
    }

    [Fact]
    public async Task Dollar_priced_products_are_converted_to_mzn_line_by_line()
    {
        var cart = (await _guest.OpenCartAsync("Maputo")).Id();
        await _guest.SetLineAsync(cart, await _guest.ProductIdAsync("Lanterna solar", "ST-LANTERNA"), 2).OkJsonAsync();
        await _guest.SetLineAsync(cart, await _guest.ProductIdAsync("Arroz agulha", "MX-ARROZ-25"), 1).OkJsonAsync();

        var pricing = await _guest.PricingAsync(cart);
        var lines = pricing.GetProperty("lines").EnumerateArray().ToList();

        var lantern = lines.Single(l => l.GetProperty("sku").GetString() == "ST-LANTERNA");
        var rate = lantern.GetProperty("exchangeRate").GetDecimal();
        rate.ShouldBeInRange(63.5m, 64.3m);
        lantern.GetProperty("currency").GetString().ShouldBe("USD");
        lantern.GetProperty("lineTotalMzn").GetDecimal().ShouldBe(decimal.Round(37m * rate, 2, MidpointRounding.AwayFromZero));

        lines.Single(l => l.GetProperty("sku").GetString() == "MX-ARROZ-25").GetProperty("exchangeRate").GetDecimal().ShouldBe(1m); // NoExchangeRate
        pricing.GetProperty("currency").GetString().ShouldBe("MZN");
    }

    [Fact]
    public async Task B2B_customers_get_their_promotion_and_carts_stay_private()
    {
        var restaurant = factory.CreateClientAs("restaurante-costa-do-sol", KnownRoles.B2B);
        var cart = (await restaurant.OpenCartAsync("Matola")).Id();
        await restaurant.SetLineAsync(cart, await restaurant.ProductIdAsync("Arroz agulha", "MX-ARROZ-25"), 1).OkJsonAsync();
        await restaurant.SetLineAsync(cart, await restaurant.ProductIdAsync("Óleo de cozinha", "MX-OLEO-5"), 1).OkJsonAsync();

        var pricing = await restaurant.PricingAsync(cart);

        pricing.GetProperty("customerType").GetString().ShouldBe("b2b");
        pricing.GetProperty("promotion").GetProperty("discount").GetDecimal().ShouldBe(77.10m); // 3 % of 2 570
        (await _guest.GetAsync(new Uri($"/api/carts/{cart}", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await factory.CreateClientAs("outro-cliente").GetAsync(new Uri($"/api/carts/{cart}", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Products_cannot_be_added_beyond_available_stock()
    {
        var cart = (await _guest.OpenCartAsync("Maputo")).Id();

        var response = await _guest.SetLineAsync(cart, await _guest.ProductIdAsync("Sal iodado", "MX-SAL-1"), 1);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("cart.insufficient_stock");
    }
}
