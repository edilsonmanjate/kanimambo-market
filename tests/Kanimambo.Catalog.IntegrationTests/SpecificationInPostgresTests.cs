using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Search;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Catalog.IntegrationTests;

/// <summary>Proves the composed specifications are translated to SQL and executed by PostgreSQL.</summary>
public sealed class SpecificationInPostgresTests(CatalogApiFactory factory)
{
    private static readonly Dictionary<Currency, decimal> Rates = new() { [Currency.Mzn] = 1m, [Currency.Zar] = 3.55m, [Currency.Usd] = 63.9m };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Specification<CatalogEntry> GroceriesDeliveredToMatola =>
        new InStock() & new InCategory("Mercearia") & new DeliversTo("matola") & new SellerRatingAtLeast(4m);

    [Fact]
    public async Task The_composed_filter_becomes_a_single_sql_where_clause()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var sql = db.CatalogEntries.Where(GroceriesDeliveredToMatola & PriceBetweenMzn.Create(500m, 2_000m, Rates)).ToQueryString();

        sql.ShouldContain("FROM catalog.catalog_entries");
        sql.ShouldContain("WHERE");
        sql.ShouldContain("\"AvailableStock\" > 0");
        sql.ShouldContain("= ANY (");          // DeliveryCities array membership
        sql.ShouldContain("\"SellerRating\" >=");
        sql.ShouldContain("\"Currency\" =");    // per-currency price bounds
    }

    [Fact]
    public async Task Postgres_returns_exactly_the_matching_seeded_items()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var search = scope.ServiceProvider.GetRequiredService<ICatalogSearch>();

        var all = await search.SearchAsync(GroceriesDeliveredToMatola & new SellerRatingAtLeast(4m), 1, 100, Ct);
        var priced = await search.SearchAsync(GroceriesDeliveredToMatola & PriceBetweenMzn.Create(500m, 2_000m, Rates), 1, 100, Ct);

        // Sal (stock 0), the Beira and Nampula sellers and the non-grocery items are filtered out in SQL.
        all.Items.Where(IsSeeded).Select(e => e.Sku).Order().ShouldBe(
            ["GM-ARROZ-50", "GM-FARINHA-25", "GM-OLEO-20", "IL-MASSA-500", "MX-ACUCAR-1", "MX-ARROZ-25", "MX-FARINHA-5", "MX-FEIJAO-1", "MX-OLEO-5"]);
        priced.Items.Where(IsSeeded).Select(e => e.Sku).Order().ShouldBe(
            ["GM-FARINHA-25", "MX-ARROZ-25", "MX-OLEO-5"]);
    }

    /// <summary>Other tests create their own sellers in the same database; assertions stay on the seed data.</summary>
    internal static bool IsSeeded(CatalogEntry entry) => CatalogSeeder.SellerIds.Contains(entry.SellerId);

    [Fact]
    public async Task The_view_derives_bundle_figures_exactly_like_the_domain_composite()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ICatalogItemRepository>();

        foreach (var id in new[] { CatalogSeeder.CabazDoMes, CatalogSeeder.CabazFamilia })
        {
            var entry = await db.CatalogEntries.SingleAsync(e => e.Id == id.Value, Ct);
            var bundle = (Bundle)(await repository.GetTreeAsync(id, Ct))!;

            entry.Kind.ShouldBe("Bundle");
            entry.AvailableStock.ShouldBe(bundle.AvailableStock);
            entry.Price.ShouldBe(bundle.Price.Amount);
            entry.WeightKg.ShouldBe(bundle.WeightKg);
        }

        // Cabaz Família needs 10 kg of sugar (5 direct + 5 via Cabaz do Mês): 300 / 10 = 30.
        (await db.CatalogEntries.SingleAsync(e => e.Id == CatalogSeeder.CabazFamilia.Value, Ct)).AvailableStock.ShouldBe(30);
    }

    [Fact]
    public async Task Nested_bundles_load_as_a_full_tree_from_the_database()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICatalogItemRepository>();

        var familia = (Bundle)(await repository.GetTreeAsync(CatalogSeeder.CabazFamilia, Ct))!;

        var inner = familia.Components.Select(c => c.Component).OfType<Bundle>().ShouldHaveSingleItem();
        inner.Components.Select(c => c.Component.Sku).Order().ShouldBe(["MX-ACUCAR-1", "MX-ARROZ-25", "MX-FARINHA-5", "MX-OLEO-5"]);
        familia.Price.ShouldBe(Money.Mzn(4_780m));
    }
}
