using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Catalog.IntegrationTests;

public sealed class ImportApiTests(CatalogApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Csv_upload_persists_valid_rows_updates_existing_skus_and_reports_rejections()
    {
        var (seller, _) = await factory.CreateSellerAsync(("EXISTENTE", 100m));
        const string csv = """
            sku;name;category;packaging;pack_size;unit;size;color;price;currency;stock;weight_kg;tax_category
            NOVO-1;Arroz;Mercearia;saco;25;kg;;;1.850,00;MZN;10;25;exempt
            EXISTENTE;Produto actualizado;Mercearia;pacote;1;kg;;;120,00;MZN;7;1;standard
            MAU-1;Sem preço;Mercearia;saco;25;kg;;;;MZN;10;25;exempt
            """;

        var report = await Upload(seller.Value, "catalogo.csv", csv);

        report.GetProperty("created").GetInt32().ShouldBe(1);
        report.GetProperty("updated").GetInt32().ShouldBe(1);
        report.GetProperty("rejected").EnumerateArray().Single().GetProperty("line").GetInt32().ShouldBe(4);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var products = await db.CatalogItems.OfType<SimpleProduct>().Where(p => p.SellerId == seller).ToListAsync(Ct);
        products.Single(p => p.Sku == "EXISTENTE").UnitPrice.Amount.ShouldBe(120m);
        products.Single(p => p.Sku == "NOVO-1").Variant.Describe().ShouldBe("saco 25 kg");
    }

    [Fact]
    public async Task An_unreadable_file_fails_the_whole_import_and_persists_nothing()
    {
        var (seller, _) = await factory.CreateSellerAsync();

        var response = await PostFile(seller.Value, "catalogo.xml", "<catalogo><artigo codigo=\"A\">");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("catalog.import_unreadable");
    }

    [Fact]
    public async Task Unsupported_file_types_are_rejected()
    {
        var (seller, _) = await factory.CreateSellerAsync();

        (await PostFile(seller.Value, "catalogo.pdf", "%PDF")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Importing_from_the_wholesaler_api_streams_every_page_and_shares_variant_attributes()
    {
        var (seller, _) = await factory.CreateSellerAsync();

        var response = await _client.PostAsync(new Uri($"/api/catalog/sellers/{seller.Value}/imports/wholesaler", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        report.GetProperty("created").GetInt32().ShouldBe(2_400);
        report.GetProperty("rejected").GetArrayLength().ShouldBe(0);

        // Flyweight through EF materialization: 2 400 SKUs, only a handful of attribute objects.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var products = await db.CatalogItems.AsNoTracking().OfType<SimpleProduct>().Where(p => p.SellerId == seller).ToListAsync(Ct);
        products.Count.ShouldBe(2_400);
        products.Select(p => p.Variant).Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBeLessThanOrEqualTo(20);
    }

    private async Task<JsonElement> Upload(Guid sellerId, string fileName, string content)
    {
        var response = await PostFile(sellerId, fileName, content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<HttpResponseMessage> PostFile(Guid sellerId, string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return await _client.PostAsync(new Uri($"/api/catalog/sellers/{sellerId}/imports", UriKind.Relative), form, Ct);
    }
}
