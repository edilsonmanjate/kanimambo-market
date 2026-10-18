namespace Kanimambo.Catalog.Application.Abstractions;

/// <summary>One product as published by the wholesaler's catalog API.</summary>
public sealed record WholesalerProduct(
    string Code,
    string Description,
    string Category,
    string UnitCode,
    long PriceCents,
    string Currency,
    int Stock,
    int WeightGrams,
    string TaxCategory);
