using System.Globalization;
using Kanimambo.Catalog.Domain.Variants;

namespace Kanimambo.Benchmarks.Flyweight;

/// <summary>
/// What BenchmarkDotNet's allocation column cannot show: memory still held once the import rows are gone.
/// Duplicated variants keep every row's strings alive; shared variants keep only the pooled instances.
/// </summary>
public static class RetainedMemory
{
    public static void Report(int count)
    {
        var duplicated = Measure(() => VariantRows.Duplicate(VariantRows.Generate(count)));
        var shared = Measure(() => VariantRows.Share(VariantRows.Generate(count), new VariantAttributesPool()));

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"""

            Retained heap after materializing {count:N0} variants (rows discarded, full GC):
            | Strategy                      | Retained   | Per variant |
            |-------------------------------|-----------:|------------:|
            | Duplicated attributes         | {duplicated / 1_048_576.0,7:0.00} MB | {duplicated / (double)count,8:0.0} B |
            | Flyweight (pooled attributes) | {shared / 1_048_576.0,7:0.00} MB | {shared / (double)count,8:0.0} B |
            Reduction: {1 - (shared / (double)duplicated):P1} ({VariantRows.DistinctCombinations} distinct attribute combinations)
            """));
    }

    private static long Measure<T>(Func<T> materialize)
    {
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var kept = materialize();
        var after = GC.GetTotalMemory(forceFullCollection: true);
        GC.KeepAlive(kept);
        return after - before;
    }
}
