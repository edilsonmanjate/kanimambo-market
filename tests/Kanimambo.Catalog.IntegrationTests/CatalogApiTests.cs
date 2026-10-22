using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Catalog.Infrastructure.Persistence;

namespace Kanimambo.Catalog.IntegrationTests;

public sealed class CatalogApiTests(CatalogApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Search_combines_query_string_filters()
    {
        // Every page: other tests in this database (the 2 400-SKU wholesaler import) add products that match too,
        // so the demo sellers' products are not guaranteed to be on page 1.
        var items = new List<JsonElement>();
        for (var number = 1; ; number++)
        {
            var page = await GetJson($"/api/catalog/products?inStock=true&category=mercearia&deliversTo=Matola&minSellerRating=4&minPrice=500&maxPrice=2000&pageSize=100&page={number}");
            items.AddRange(page.GetProperty("items").EnumerateArray());
            if (items.Count >= page.GetProperty("totalCount").GetInt32() || page.GetProperty("items").GetArrayLength() == 0)
            {
                break;
            }
        }

        items
            .Where(i => CatalogSeeder.SellerIds.Contains(i.GetProperty("sellerId").GetGuid()))
            .Select(i => i.GetProperty("sku").GetString())
            .Order().ShouldBe(["GM-FARINHA-25", "MX-ARROZ-25", "MX-OLEO-5"]);
    }

    [Fact]
    public async Task Prices_in_foreign_currencies_are_shown_in_mzn_too()
    {
        var page = await GetJson("/api/catalog/products?q=lanterna");

        var lantern = page.GetProperty("items").EnumerateArray().Single();
        lantern.GetProperty("currency").GetString().ShouldBe("USD");
        lantern.GetProperty("priceMzn").GetDecimal().ShouldBeInRange(18.5m * 63.5m, 18.5m * 64.3m);
    }

    [Fact]
    public async Task Bundle_details_show_the_tree_stock_and_iva_per_tax_category()
    {
        var details = await GetJson($"/api/catalog/products/{CatalogSeeder.CabazFamilia.Value}");

        details.GetProperty("kind").GetString().ShouldBe("Bundle");
        details.GetProperty("availableStock").GetInt32().ShouldBe(30);
        details.GetProperty("price").GetProperty("amount").GetDecimal().ShouldBe(4_780m);
        details.GetProperty("priceMzn").GetProperty("amount").GetDecimal().ShouldBe(4_780m);

        var iva = details.GetProperty("iva");
        var lines = iva.GetProperty("lines").EnumerateArray()
            .Select(l => (l.GetProperty("category").GetString(), l.GetProperty("net").GetProperty("amount").GetDecimal(), l.GetProperty("iva").GetProperty("amount").GetDecimal()))
            .ToList();
        lines.ShouldBe([("exempt", 2_470m, 0m), ("standard", 1_991.38m, 318.62m)]);   // 2 310 IVA included, 16 %
        iva.GetProperty("gross").GetProperty("amount").GetDecimal().ShouldBe(4_780m);       // = the price: IVA is included

        var components = details.GetProperty("components").EnumerateArray().ToList();
        components.Count.ShouldBe(3);
        components.Single(c => c.GetProperty("kind").GetString() == "Bundle").GetProperty("components").GetArrayLength().ShouldBe(4);
    }

    [Fact]
    public async Task Unknown_products_are_a_404_problem()
    {
        var response = await _client.GetAsync(new Uri($"/api/catalog/products/{Guid.NewGuid()}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("catalog.product_not_found");
    }

    [Fact]
    public async Task Sellers_can_compose_new_bundles_from_their_products()
    {
        var (seller, products) = await factory.CreateSellerAsync(("ARROZ", 400m), ("OLEO", 150m));

        var response = await _client.PostAsJsonAsync($"/api/catalog/sellers/{seller.Value}/bundles", new
        {
            sku = "CABAZ-SEMANA",
            name = "Cabaz da Semana",
            category = "Cabazes",
            components = new[] { new { productId = products[0].Id.Value, quantity = 2 }, new { productId = products[1].Id.Value, quantity = 1 } },
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await _client.GetFromJsonAsync<JsonElement>(response.Headers.Location, Ct);
        created.GetProperty("price").GetProperty("amount").GetDecimal().ShouldBe(950m);
        created.GetProperty("availableStock").GetInt32().ShouldBe(5);
    }

    private async Task<JsonElement> GetJson(string path) =>
        await _client.GetFromJsonAsync<JsonElement>(new Uri(path, UriKind.Relative), Ct);
}
