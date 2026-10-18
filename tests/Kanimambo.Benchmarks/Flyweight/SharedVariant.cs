using Kanimambo.Catalog.Domain.Variants;

namespace Kanimambo.Benchmarks.Flyweight;

/// <summary>With the pattern: extrinsic state per product, intrinsic state shared through the pool.</summary>
public sealed record SharedVariant(string Sku, decimal Price, int Stock, VariantAttributes Attributes);
