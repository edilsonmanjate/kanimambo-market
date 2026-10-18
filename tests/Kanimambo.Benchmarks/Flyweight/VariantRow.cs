namespace Kanimambo.Benchmarks.Flyweight;

/// <summary>One parsed import row. Every row has its own string instances, as a CSV/XML parser produces.</summary>
public sealed record VariantRow(string Sku, decimal Price, int Stock, string Packaging, decimal PackSize, string Unit, string? Size, string? Color);
