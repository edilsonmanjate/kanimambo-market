using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Cart.Domain;
using Kanimambo.Cart.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Kanimambo.Cart.IntegrationTests.CartApi;

namespace Kanimambo.Cart.IntegrationTests;

public sealed class CartHistoryApiTests(CartApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Every_change_is_snapshotted_and_snapshot_n_can_be_restored()
    {
        var cart = (await _client.OpenCartAsync("Maputo")).Id();
        var arroz = await _client.ProductIdAsync("Arroz agulha", "MX-ARROZ-25");
        var oleo = await _client.ProductIdAsync("Óleo de cozinha", "MX-OLEO-5");

        await _client.SetLineAsync(cart, arroz, 1).OkJsonAsync();                     // v2
        await _client.SetLineAsync(cart, oleo, 2).OkJsonAsync();                      // v3
        await _client.SetLineAsync(cart, arroz, 3).OkJsonAsync();                     // v4
        await _client.PutAsJsonAsync($"/api/carts/{cart}/delivery-city", new { city = "Matola" }, Ct).OkJsonAsync(); // v5

        (await Versions(cart)).ShouldBe([1, 2, 3, 4, 5]);

        var restored = await _client.PostAsync(new Uri($"/api/carts/{cart}/snapshots/3/restore", UriKind.Relative), null, Ct).OkJsonAsync();

        restored.GetProperty("version").GetInt32().ShouldBe(3);
        restored.GetProperty("deliveryCity").GetString().ShouldBe("Maputo");
        Quantities(restored).ShouldBe([("MX-ARROZ-25", 1), ("MX-OLEO-5", 2)]);
        (await Versions(cart)).ShouldBe([1, 2, 3]);

        // History is a linear stack: the next change becomes the new version 4.
        var next = await _client.SetLineAsync(cart, oleo, 0).OkJsonAsync();
        next.GetProperty("version").GetInt32().ShouldBe(4);
        (await Versions(cart)).ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public async Task Undo_goes_back_one_change_at_a_time_until_the_empty_cart()
    {
        var cart = (await _client.OpenCartAsync("Beira")).Id();
        var arroz = await _client.ProductIdAsync("Arroz agulha", "MX-ARROZ-25");
        await _client.SetLineAsync(cart, arroz, 1).OkJsonAsync();
        await _client.SetLineAsync(cart, arroz, 4).OkJsonAsync();

        Quantities(await Undo(cart)).ShouldBe([("MX-ARROZ-25", 1)]);
        Quantities(await Undo(cart)).ShouldBeEmpty();

        var nothingLeft = await _client.PostAsync(new Uri($"/api/carts/{cart}/undo", UriKind.Relative), null, Ct);
        nothingLeft.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Snapshots_are_stored_as_json_and_only_the_latest_twenty_are_kept()
    {
        var cart = (await _client.OpenCartAsync("Nampula")).Id();
        var arroz = await _client.ProductIdAsync("Arroz agulha", "MX-ARROZ-25");
        for (var quantity = 1; quantity <= 25; quantity++)
        {
            await _client.SetLineAsync(cart, arroz, quantity % 30).OkJsonAsync();
        }

        var versions = await Versions(cart);
        versions.Count.ShouldBe(20);
        versions[0].ShouldBe(7);
        versions[^1].ShouldBe(26);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        var payload = await db.Database.SqlQuery<string>($"SELECT \"Payload\"::text AS \"Value\" FROM cart.\"CartSnapshots\" WHERE \"CartId\" = {cart} AND \"Version\" = 26").SingleAsync(Ct);
        JsonDocument.Parse(payload).RootElement.GetProperty("lines")[0].GetProperty("quantity").GetInt32().ShouldBe(25);
    }

    [Fact]
    public async Task Two_devices_saving_the_same_cart_version_conflict_instead_of_overwriting()
    {
        var cart = (await _client.OpenCartAsync("Maputo")).Id();

        await using var phone = factory.Services.CreateAsyncScope();
        await using var tablet = factory.Services.CreateAsyncScope();
        var phoneDb = phone.ServiceProvider.GetRequiredService<CartDbContext>();
        var tabletDb = tablet.ServiceProvider.GetRequiredService<CartDbContext>();

        var onPhone = await phoneDb.Carts.SingleAsync(c => c.Id == new CartId(cart), Ct);
        var onTablet = await tabletDb.Carts.SingleAsync(c => c.Id == new CartId(cart), Ct);

        onPhone.ChangeDeliveryCity("Matola", DateTimeOffset.UtcNow);
        await phoneDb.SaveChangesAsync(Ct);

        onTablet.ChangeDeliveryCity("Beira", DateTimeOffset.UtcNow);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => tabletDb.SaveChangesAsync(Ct));
    }

    private async Task<JsonElement> Undo(Guid cart) =>
        await _client.PostAsync(new Uri($"/api/carts/{cart}/undo", UriKind.Relative), null, Ct).OkJsonAsync();

    private async Task<List<int>> Versions(Guid cart) =>
        (await _client.GetFromJsonAsync<JsonElement>(new Uri($"/api/carts/{cart}/snapshots", UriKind.Relative), Ct))
            .EnumerateArray().Select(s => s.GetProperty("version").GetInt32()).ToList();

    private static List<(string?, int)> Quantities(JsonElement cart) =>
        cart.GetProperty("lines").EnumerateArray().Select(l => (l.GetProperty("sku").GetString(), l.GetProperty("quantity").GetInt32())).ToList();
}
