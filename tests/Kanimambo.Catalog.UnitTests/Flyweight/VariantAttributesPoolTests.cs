using Kanimambo.Catalog.Domain.Variants;

namespace Kanimambo.Catalog.UnitTests.Flyweight;

public sealed class VariantAttributesPoolTests
{
    [Fact]
    public void Equal_attributes_resolve_to_the_same_shared_instance()
    {
        var pool = new VariantAttributesPool();

        var first = pool.Get("saco", 25m, "kg").Value;
        var second = pool.Get(" SACO ", 25.0m, "KG").Value;

        second.ShouldBeSameAs(first);
        first.Describe().ShouldBe("saco 25 kg");
        pool.Count.ShouldBe(1);
    }

    [Fact]
    public void Different_attributes_are_different_instances()
    {
        var pool = new VariantAttributesPool();

        pool.Get("saco", 25m, "kg").Value.ShouldNotBeSameAs(pool.Get("saco", 50m, "kg").Value);
        pool.Get("unidade", 1m, "un", color: "azul").Value.ShouldNotBeSameAs(pool.Get("unidade", 1m, "un", color: "vermelho").Value);
        pool.Count.ShouldBe(4);
    }

    [Fact]
    public void The_persisted_form_materializes_back_into_the_shared_instance()
    {
        var pool = new VariantAttributesPool();
        var original = pool.Get("unidade", 2m, "m", size: null, color: "azul").Value;

        var stored = original.ToStorageString();

        stored.ShouldBe("unidade|2|m||azul");
        pool.FromStorageString(stored).ShouldBeSameAs(original);
        Should.Throw<FormatException>(() => pool.FromStorageString("garbage"));
    }

    [Fact]
    public void Invalid_attributes_are_validation_errors()
    {
        var pool = new VariantAttributesPool();

        pool.Get("", 1m, "kg").IsFailure.ShouldBeTrue();
        pool.Get("saco", 0m, "kg").IsFailure.ShouldBeTrue();
        pool.Get("saco", 1m, " ").IsFailure.ShouldBeTrue();
        pool.Count.ShouldBe(0);
    }

    [Fact]
    public void One_hundred_thousand_skus_share_a_handful_of_attribute_objects()
    {
        var pool = new VariantAttributesPool();
        var packs = new (string Packaging, decimal Size, string Unit)[]
        {
            ("saco", 5m, "kg"), ("saco", 25m, "kg"), ("saco", 50m, "kg"), ("caixa", 12m, "un"), ("bidão", 5m, "l"),
        };

        var variants = Enumerable.Range(0, 100_000)
            .Select(i => packs[i % packs.Length])
            .Select(p => pool.Intern(p.Packaging, p.Size, p.Unit))
            .ToList();

        variants.Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(packs.Length);
        pool.Count.ShouldBe(packs.Length);
    }

    [Fact]
    public void Looking_up_an_existing_combination_does_not_allocate_per_lookup()
    {
        var pool = new VariantAttributesPool();
        const string packaging = "saco", unit = "kg";
        for (var i = 0; i < 1_000; i++)
        {
            pool.Intern(packaging, 25m, unit); // warm up (JIT, first insert)
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            pool.Intern(packaging, 25m, unit);
        }

        // A naive 'new VariantAttributes' per row would allocate ~5 MB here; the pool allocates nothing per call.
        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBeLessThan(1_024);
    }
}
