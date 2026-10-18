using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Contracts;

/// <summary>What another module may know about a sellable item right now (price, stock derived for bundles).</summary>
public sealed record CatalogItemSnapshot(
    Guid ProductId,
    Guid SellerId,
    string Sku,
    string Name,
    string Category,
    Money Price,
    int AvailableStock,
    decimal WeightKg);
