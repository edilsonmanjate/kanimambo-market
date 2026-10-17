using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Catalog.IntegrationTests;

public sealed class BackofficeApiTests(CatalogApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _backoffice = factory.CreateClientAs("ana.backoffice", AuthorizationPolicies.BackofficeRole);

    [Fact]
    public async Task Anonymous_and_non_backoffice_users_are_turned_away()
    {
        var (seller, _) = await factory.CreateSellerAsync(("X", 10m));
        var path = $"/api/catalog/backoffice/sellers/{seller.Value}/price-adjustments";
        var body = new { percentage = 10, reason = "test" };

        (await factory.CreateClient().PostAsJsonAsync(path, body, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await factory.CreateClientAs("cliente").PostAsJsonAsync(path, body, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_price_adjustment_is_audited_and_can_be_undone_exactly_once()
    {
        var (seller, _) = await factory.CreateSellerAsync(("ARROZ", 1_850m), ("OLEO", 720m));

        var executed = await Send($"/api/catalog/backoffice/sellers/{seller.Value}/price-adjustments", new { percentage = 10, reason = "Desvalorização do metical" });
        (await PricesOf(seller.Value)).ShouldBe([2_035m, 792m]);
        executed.GetProperty("performedBy").GetString().ShouldBe("ana.backoffice");

        var operationId = executed.GetProperty("id").GetGuid();
        var log = await _backoffice.GetFromJsonAsync<JsonElement>(new Uri("/api/catalog/backoffice/operations?pageSize=100", UriKind.Relative), Ct);
        log.GetProperty("items").EnumerateArray().ShouldContain(o => o.GetProperty("id").GetGuid() == operationId);

        var undone = await Send($"/api/catalog/backoffice/operations/{operationId}/undo", null);
        undone.GetProperty("status").GetString().ShouldBe("Undone");
        undone.GetProperty("undoneBy").GetString().ShouldBe("ana.backoffice");
        (await PricesOf(seller.Value)).ShouldBe([1_850m, 720m]);

        var again = await _backoffice.PostAsync(new Uri($"/api/catalog/backoffice/operations/{operationId}/undo", UriKind.Relative), null, Ct);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_suspended_seller_disappears_from_search_until_the_suspension_is_undone()
    {
        var (seller, _) = await factory.CreateSellerAsync(("UNICO-SKU-SUSPENSO", 99m));
        var client = factory.CreateClient();

        async Task<int> SearchCount() =>
            (await client.GetFromJsonAsync<JsonElement>(new Uri("/api/catalog/products?q=UNICO-SKU-SUSPENSO", UriKind.Relative), Ct))
                .GetProperty("totalCount").GetInt32();

        (await SearchCount()).ShouldBe(1);

        var suspension = await Send($"/api/catalog/backoffice/sellers/{seller.Value}/suspensions", new { reason = "Investigação de fraude" });
        (await SearchCount()).ShouldBe(0);

        await Send($"/api/catalog/backoffice/operations/{suspension.GetProperty("id").GetGuid()}/undo", null);
        (await SearchCount()).ShouldBe(1);
    }

    private async Task<JsonElement> Send(string path, object? body)
    {
        var response = body is null
            ? await _backoffice.PostAsync(new Uri(path, UriKind.Relative), null, Ct)
            : await _backoffice.PostAsJsonAsync(path, body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<decimal[]> PricesOf(Guid sellerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await db.CatalogItems.AsNoTracking().OfType<SimpleProduct>()
            .Where(p => p.SellerId == new Domain.SellerId(sellerId))
            .OrderBy(p => p.Sku)
            .Select(p => p.UnitPrice.Amount)
            .ToArrayAsync(Ct);
    }
}
