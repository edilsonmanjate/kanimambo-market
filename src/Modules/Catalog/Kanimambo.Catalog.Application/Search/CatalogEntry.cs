namespace Kanimambo.Catalog.Application.Search;

/// <summary>
/// Read model of a sellable item, flattened for search: product data plus the seller attributes the
/// filters need. Bundle price, weight and stock are pre-computed (recursively) by the database view.
/// </summary>
public sealed class CatalogEntry
{
    public Guid Id { get; init; }

    public Guid SellerId { get; init; }

    public string SellerName { get; init; } = string.Empty;

    public string SellerCity { get; init; } = string.Empty;

    public decimal SellerRating { get; init; }

    public IReadOnlyList<string> DeliveryCities { get; init; } = [];

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string Currency { get; init; } = string.Empty;

    public int AvailableStock { get; init; }

    public decimal WeightKg { get; init; }
}
