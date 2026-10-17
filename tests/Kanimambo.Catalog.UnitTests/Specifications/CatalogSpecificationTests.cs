using Kanimambo.Catalog.Application.Search;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.UnitTests.Specifications;

public sealed class CatalogSpecificationTests
{
    private static readonly CatalogEntry Arroz = Entry("Arroz agulha 25 kg", 1_850m, "MZN", stock: 40, category: "mercearia", rating: 4.6m, "Maputo", "Matola");
    private static readonly CatalogEntry Sal = Entry("Sal iodado", 35m, "MZN", stock: 0, category: "mercearia", rating: 4.6m, "Maputo", "Matola");
    private static readonly CatalogEntry Lanterna = Entry("Lanterna solar", 18.5m, "USD", stock: 120, category: "energia-solar", rating: 4.4m, "Maputo", "Beira");
    private static readonly CatalogEntry Massa = Entry("Massa esparguete", 22.9m, "ZAR", stock: 300, category: "mercearia", rating: 4.0m, "Matola");
    private static readonly CatalogEntry Peixe = Entry("Peixe seco", 420m, "MZN", stock: 35, category: "frescos", rating: 3.8m, "Beira");

    private static readonly CatalogEntry[] Catalog = [Arroz, Sal, Lanterna, Massa, Peixe];

    private static readonly Dictionary<Currency, decimal> Rates = new() { [Currency.Mzn] = 1m, [Currency.Usd] = 64m, [Currency.Zar] = 3.5m };

    [Fact]
    public void Filters_compose_like_the_catalog_search_screen()
    {
        var spec = new InStock() & new InCategory("Mercearia") & new DeliversTo("matola") & new SellerRatingAtLeast(4m);

        Catalog.Where(spec.IsSatisfiedBy).ShouldBe([Arroz, Massa]);
    }

    [Fact]
    public void Price_in_mzn_compares_each_currency_with_converted_bounds()
    {
        // 18.50 USD × 64 = 1 184 MZN; 22.90 ZAR × 3.5 = 80.15 MZN.
        var spec = PriceBetweenMzn.Create(1_000m, 2_000m, Rates);

        Catalog.Where(spec.IsSatisfiedBy).ShouldBe([Arroz, Lanterna]);
    }

    [Fact]
    public void Items_in_a_currency_without_a_rate_today_are_left_out()
    {
        var withoutUsd = new Dictionary<Currency, decimal> { [Currency.Mzn] = 1m };

        Catalog.Where(PriceBetweenMzn.Create(1_000m, 2_000m, withoutUsd).IsSatisfiedBy).ShouldBe([Arroz]);
    }

    [Fact]
    public void Open_ended_price_ranges_are_supported()
    {
        Catalog.Where(PriceBetweenMzn.Create(null, 100m, Rates).IsSatisfiedBy).ShouldBe([Sal, Massa]);
    }

    [Fact]
    public void Name_search_is_case_insensitive()
    {
        Catalog.Where(new NameContains("ARROZ").IsSatisfiedBy).ShouldBe([Arroz]);
    }

    [Fact]
    public void Starting_from_all_lets_the_handler_add_only_the_filters_that_were_requested()
    {
        var spec = Specification<CatalogEntry>.All;
        Catalog.Where(spec.IsSatisfiedBy).Count().ShouldBe(Catalog.Length);

        spec &= new InCategory("frescos");
        Catalog.Where(spec.IsSatisfiedBy).ShouldBe([Peixe]);
    }

    private static CatalogEntry Entry(string name, decimal price, string currency, int stock, string category, decimal rating, params string[] cities) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Price = price,
            Currency = currency,
            AvailableStock = stock,
            Category = category,
            SellerRating = rating,
            DeliveryCities = cities,
        };
}
