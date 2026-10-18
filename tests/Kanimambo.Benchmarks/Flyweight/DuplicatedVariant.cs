namespace Kanimambo.Benchmarks.Flyweight;

/// <summary>Without the pattern: every product carries its own copy of the variant attributes.</summary>
public sealed record DuplicatedVariant(string Sku, decimal Price, int Stock, string Packaging, decimal PackSize, string Unit, string? Size, string? Color);
