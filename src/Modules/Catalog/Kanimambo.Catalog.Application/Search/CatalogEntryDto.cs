namespace Kanimambo.Catalog.Application.Search;

public sealed record CatalogEntryDto(
    Guid Id,
    string Sku,
    string Name,
    string Category,
    string Kind,
    decimal Price,
    string Currency,
    decimal? PriceMzn,
    int AvailableStock,
    decimal WeightKg,
    Guid SellerId,
    string SellerName,
    string SellerCity,
    decimal SellerRating);
