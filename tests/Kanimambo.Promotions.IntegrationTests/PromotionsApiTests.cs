using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Promotions.IntegrationTests;
using Kanimambo.SharedKernel.Identity;

[assembly: AssemblyFixture(typeof(PromotionsApiFactory))]

namespace Kanimambo.Promotions.IntegrationTests;

public sealed class PromotionsApiTests(PromotionsApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _marketing = factory.CreateClientAs("marta.marketing", KnownRoles.Backoffice);

    [Fact]
    public async Task Only_backoffice_staff_manage_promotions()
    {
        (await factory.CreateClient().GetAsync(new Uri("/api/promotions", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await factory.CreateClientAs("cliente").GetAsync(new Uri("/api/promotions", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_rule_with_a_syntax_error_is_rejected_with_its_position()
    {
        var response = await _marketing.PostAsJsonAsync("/api/promotions", new
        {
            name = "Errada",
            rule = "cart.total >= 2500 AN customer.city = \"Maputo\"",
            discountKind = "Percentage",
            discountValue = 5,
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").GetProperty("rule")[0].GetString().ShouldBe("Position 20: Unexpected 'AN': expected AND, OR or end of rule.");
    }

    [Fact]
    public async Task The_editor_can_validate_a_rule_and_get_a_caret_excerpt()
    {
        var invalid = await Validate("custmer.city = \"Beira\"");
        var valid = await Validate("cart.total>=1000 and customer.city in [\"Beira\",\"Dondo\"]");

        invalid.GetProperty("isValid").GetBoolean().ShouldBeFalse();
        invalid.GetProperty("error").GetProperty("position").GetInt32().ShouldBe(1);
        invalid.GetProperty("error").GetProperty("message").GetString().ShouldBe("Unknown variable 'custmer.city'. Did you mean 'customer.city'?");
        invalid.GetProperty("error").GetProperty("excerpt").GetString().ShouldBe("custmer.city = \"Beira\"\n^^^^^^^^^^^^");

        valid.GetProperty("isValid").GetBoolean().ShouldBeTrue();
        valid.GetProperty("canonicalRule").GetString().ShouldBe("cart.total >= 1000 AND customer.city IN [\"Beira\", \"Dondo\"]");
    }

    [Fact]
    public async Task A_stored_rule_is_evaluated_at_runtime_against_real_carts_until_deactivated()
    {
        var promotion = await Json(_marketing.PostAsJsonAsync("/api/promotions", new
        {
            name = "Chimoio: 10% a partir de 2 unidades",
            rule = "customer.city = \"Chimoio\" AND cart.itemCount >= 2",
            discountKind = "Percentage",
            discountValue = 10,
        }, Ct));
        promotion.GetProperty("rule").GetString().ShouldBe("customer.city = \"Chimoio\" AND cart.itemCount >= 2");

        var shopper = factory.CreateClient();
        var cart = await Json(shopper.PostAsJsonAsync("/api/carts", new { deliveryCity = "Chimoio" }, Ct), HttpStatusCode.Created);
        var cartId = cart.GetProperty("id").GetGuid();
        var page = await shopper.GetFromJsonAsync<JsonElement>(new Uri("/api/catalog/products?q=Coco", UriKind.Relative), Ct);
        var coco = page.GetProperty("items")[0].GetProperty("id").GetGuid();
        await Json(shopper.PutAsJsonAsync($"/api/carts/{cartId}/lines/{coco}", new { quantity = 2 }, Ct));

        var priced = await shopper.GetFromJsonAsync<JsonElement>(new Uri($"/api/carts/{cartId}/pricing", UriKind.Relative), Ct);
        priced.GetProperty("promotion").GetProperty("name").GetString().ShouldBe("Chimoio: 10% a partir de 2 unidades");
        priced.GetProperty("promotion").GetProperty("discount").GetDecimal().ShouldBe(8m); // 10 % of 2 × 40

        var id = promotion.GetProperty("id").GetGuid();
        (await Json(_marketing.PostAsync(new Uri($"/api/promotions/{id}/deactivate", UriKind.Relative), null, Ct))).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await _marketing.PostAsync(new Uri($"/api/promotions/{id}/deactivate", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var after = await shopper.GetFromJsonAsync<JsonElement>(new Uri($"/api/carts/{cartId}/pricing", UriKind.Relative), Ct);
        after.GetProperty("promotion").GetProperty("discount").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task The_seeded_promotions_and_the_rule_vocabulary_are_listed()
    {
        var promotions = await _marketing.GetFromJsonAsync<JsonElement>(new Uri("/api/promotions?activeOnly=true", UriKind.Relative), Ct);
        var variables = await _marketing.GetFromJsonAsync<JsonElement>(new Uri("/api/promotions/rules/variables", UriKind.Relative), Ct);

        promotions.EnumerateArray().Select(p => p.GetProperty("name").GetString()).ShouldContain("Cabazes: 150 MZN de desconto");
        promotions.EnumerateArray().First().GetProperty("discountKind").GetString().ShouldBe("Percentage");
        variables.GetArrayLength().ShouldBe(7);
    }

    private async Task<JsonElement> Validate(string rule) =>
        await Json(_marketing.PostAsJsonAsync("/api/promotions/rules/validate", new { rule }, Ct));

    private static async Task<JsonElement> Json(Task<HttpResponseMessage> request, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await request;
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
