using BenchmarkDotNet.Attributes;
using Kanimambo.Catalog.Domain.Variants;

namespace Kanimambo.Benchmarks.Flyweight;

/// <summary>Materializing 100 000 product variants from parsed rows, with and without the flyweight pool.</summary>
[MemoryDiagnoser]
public class FlyweightBenchmarks
{
    private VariantRow[] _rows = [];
    private VariantAttributesPool _pool = new();

    [Params(100_000)]
    public int Variants { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rows = VariantRows.Generate(Variants);
        _pool = new VariantAttributesPool();
    }

    [Benchmark(Baseline = true, Description = "Duplicated attributes")]
    public DuplicatedVariant[] Duplicated() => VariantRows.Duplicate(_rows);

    [Benchmark(Description = "Flyweight (pooled attributes)")]
    public SharedVariant[] Flyweight() => VariantRows.Share(_rows, _pool);
}
