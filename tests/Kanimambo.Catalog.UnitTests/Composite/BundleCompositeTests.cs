using Kanimambo.Catalog.Application.Pricing;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using static Kanimambo.Catalog.UnitTests.TestCatalog;

namespace Kanimambo.Catalog.UnitTests.Composite;

public sealed class BundleCompositeTests
{
    private readonly SimpleProduct _arroz = Product("ARROZ-25", 1_850m, stock: 40, weightKg: 25m, taxCategory: "exempt");
    private readonly SimpleProduct _oleo = Product("OLEO-5", 720m, stock: 60, weightKg: 4.6m);
    private readonly SimpleProduct _acucar = Product("ACUCAR-1", 95m, stock: 300, weightKg: 1m);
    private readonly SimpleProduct _farinha = Product("FARINHA-5", 310m, stock: 80, weightKg: 5m, taxCategory: "exempt");
    private readonly SimpleProduct _feijao = Product("FEIJAO-1", 160m, stock: 200, weightKg: 1m);

    private Bundle CabazDoMes() => Bundle("CABAZ-MES", (_arroz, 1), (_oleo, 1), (_acucar, 5), (_farinha, 2));

    [Fact]
    public void A_single_product_and_a_bundle_answer_the_same_questions()
    {
        CatalogItem[] items = [_arroz, CabazDoMes()];

        items.Select(i => i.Price).ShouldBe([Money.Mzn(1_850m), Money.Mzn(3_665m)]);
        items.Select(i => i.WeightKg).ShouldBe([25m, 44.6m]);
        items.Select(i => i.AvailableStock).ShouldBe([40, 40]);
    }

    [Fact]
    public void Bundle_stock_is_the_minimum_the_components_allow()
    {
        var bundle = Bundle("CABAZ", (_acucar, 5), (_oleo, 1));

        // sugar: 300 / 5 = 60 bundles, oil: 60 / 1 = 60 bundles
        bundle.AvailableStock.ShouldBe(60);
        Bundle("CABAZ-2", (_acucar, 7), (_oleo, 1)).AvailableStock.ShouldBe(42);
    }

    [Fact]
    public void Nested_bundles_aggregate_a_shared_product_before_taking_the_minimum()
    {
        // Família = Cabaz do Mês (with 5 kg sugar) + 4 feijão + 5 more kg of sugar → 10 kg sugar per unit.
        var familia = Bundle("CABAZ-FAMILIA", (CabazDoMes(), 1), (_feijao, 4), (_acucar, 5));

        // Correct: sugar 300 / 10 = 30. A naive recursive minimum would answer
        // min(cabaz do mês = 40, feijão 200 / 4 = 50, sugar 300 / 5 = 60) = 40 and oversell.
        familia.AvailableStock.ShouldBe(30);
        familia.Price.ShouldBe(Money.Mzn(3_665m + 640m + 475m));
        familia.WeightKg.ShouldBe(44.6m + 4m + 5m);
    }

    [Fact]
    public void Leaf_requirements_flatten_any_depth()
    {
        var inner = Bundle("INNER", (_acucar, 2));
        var middle = Bundle("MIDDLE", (inner, 3));
        var outer = Bundle("OUTER", (middle, 2), (_acucar, 1));

        outer.LeafRequirements(1).Sum(r => r.Quantity).ShouldBe((2 * 3 * 2) + 1);
        outer.AvailableStock.ShouldBe(300 / 13);
    }

    [Fact]
    public void Iva_is_extracted_per_component_because_rates_differ()
    {
        var iva = IvaBreakdown.Calculate(CabazDoMes().TaxableAmounts(1), FixedTaxRates.Mozambique).Value;

        iva.Lines.Select(l => (l.Category, l.Net.Amount, l.Iva.Amount)).ShouldBe([
            ("exempt", 1_850m + 620m, 0m),
            ("standard", 1_030.17m, 164.83m),   // 720 + 475 = 1 195 IVA included: 1 195 / 1.16
        ]);
        iva.Net.ShouldBe(Money.Mzn(3_500.17m));
        iva.Gross.ShouldBe(Money.Mzn(3_665m));     // prices include IVA: the gross is what the customer pays
    }

    [Fact]
    public void Unknown_tax_categories_fail_instead_of_silently_charging_zero()
    {
        var unknown = Product("X", 100m, taxCategory: "luxury");

        IvaBreakdown.Calculate(unknown.TaxableAmounts(1), FixedTaxRates.Mozambique).Error.Code.ShouldBe("tax.unknown");
    }

    [Fact]
    public void A_bundle_cannot_contain_itself_at_any_depth()
    {
        var outer = Bundle("OUTER", (_acucar, 1));
        var inner = Bundle("INNER", (outer, 1));

        var result = outer.AddComponent(inner, 1);

        result.Error.Code.ShouldBe("catalog.bundle_cycle");
        outer.AddComponent(outer, 1).Error.Code.ShouldBe("catalog.bundle_cycle");
    }

    [Fact]
    public void Components_must_share_seller_and_currency()
    {
        var bundle = Bundle("CABAZ", (_acucar, 1));

        bundle.AddComponent(Product("OTHER", 10m, seller: OtherSeller), 1).Error.Code.ShouldBe("catalog.bundle_mixed_sellers");
        bundle.AddComponent(Product("USD", 10m, currency: Currency.Usd), 1).Error.Code.ShouldBe("catalog.bundle_mixed_currencies");
    }

    [Fact]
    public void Adding_the_same_component_twice_merges_the_quantity()
    {
        var bundle = Bundle("CABAZ", (_acucar, 2));

        bundle.AddComponent(_acucar, 3);

        bundle.Components.ShouldHaveSingleItem().Quantity.ShouldBe(5);
    }

    [Fact]
    public void Bundles_need_components_with_positive_quantities()
    {
        Domain.Products.Bundle.Create(Domain.ProductId.New(), Mercearia, "EMPTY", "Vazio", "cabazes", [])
            .Error.ShouldBeOfType<ValidationError>();
        Bundle("CABAZ", (_acucar, 1)).AddComponent(_oleo, 0).Error.Type.ShouldBe(ErrorType.Validation);
    }
}
