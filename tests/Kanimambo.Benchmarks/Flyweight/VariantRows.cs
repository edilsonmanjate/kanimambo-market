using System.Globalization;
using Kanimambo.Catalog.Domain.Variants;

namespace Kanimambo.Benchmarks.Flyweight;

/// <summary>One parsed import row. Every row has its own string instances, as a CSV/XML parser produces.</summary>
public sealed record VariantRow(string Sku, decimal Price, int Stock, string Packaging, decimal PackSize, string Unit, string? Size, string? Color);

/// <summary>Without the pattern: every product carries its own copy of the variant attributes.</summary>
public sealed record DuplicatedVariant(string Sku, decimal Price, int Stock, string Packaging, decimal PackSize, string Unit, string? Size, string? Color);

/// <summary>With the pattern: extrinsic state per product, intrinsic state shared through the pool.</summary>
public sealed record SharedVariant(string Sku, decimal Price, int Stock, VariantAttributes Attributes);

public static class VariantRows
{
    private static readonly (string Packaging, decimal PackSize, string Unit, string? Size, string? Color)[] Combinations =
    [
        ("saco", 5m, "kg", null, null), ("saco", 10m, "kg", null, null), ("saco", 25m, "kg", null, null), ("saco", 50m, "kg", null, null),
        ("pacote", 500m, "g", null, null), ("pacote", 1m, "kg", null, null), ("caixa", 12m, "un", null, null), ("caixa", 24m, "un", null, null),
        ("bidão", 5m, "l", null, null), ("bidão", 20m, "l", null, null), ("garrafa", 1.5m, "l", null, null), ("unidade", 2m, "m", null, "azul"),
        ("unidade", 2m, "m", null, "vermelho"), ("unidade", 1m, "un", "M", "preto"), ("unidade", 1m, "un", "L", "preto"), ("unidade", 1m, "un", "XL", "branco"),
    ];

    public static int DistinctCombinations => Combinations.Length;

    /// <summary>Rows whose attribute strings are fresh copies, not literals, to mimic parser output.</summary>
    public static VariantRow[] Generate(int count) =>
        Enumerable.Range(0, count).Select(i =>
        {
            var c = Combinations[i % Combinations.Length];
            return new VariantRow(
                string.Create(CultureInfo.InvariantCulture, $"SKU-{i:000000}"),
                100m + (i % 900),
                i % 250,
                new string(c.Packaging.AsSpan()),
                c.PackSize,
                new string(c.Unit.AsSpan()),
                c.Size is null ? null : new string(c.Size.AsSpan()),
                c.Color is null ? null : new string(c.Color.AsSpan()));
        }).ToArray();

    public static DuplicatedVariant[] Duplicate(VariantRow[] rows) =>
        Array.ConvertAll(rows, r => new DuplicatedVariant(r.Sku, r.Price, r.Stock, r.Packaging, r.PackSize, r.Unit, r.Size, r.Color));

    public static SharedVariant[] Share(VariantRow[] rows, VariantAttributesPool pool) =>
        Array.ConvertAll(rows, r => new SharedVariant(r.Sku, r.Price, r.Stock, pool.Intern(r.Packaging, r.PackSize, r.Unit, r.Size, r.Color)));
}
