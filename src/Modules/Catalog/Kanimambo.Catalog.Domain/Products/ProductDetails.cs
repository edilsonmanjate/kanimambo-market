using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Domain.Products;

public sealed record ProductDetails(
    string Sku,
    string Name,
    string Category,
    VariantAttributes Variant,
    Money Price,
    int Stock,
    decimal WeightKg,
    TaxCategory TaxCategory);
