namespace Kanimambo.Catalog.Application.Products;

public sealed record ProductDetailsDto(
    Guid Id,
    Guid SellerId,
    string Sku,
    string Name,
    string Category,
    string Kind,
    string? Variant,
    MoneyDto Price,
    MoneyDto? PriceMzn,
    decimal? ExchangeRateToMzn,
    decimal WeightKg,
    int AvailableStock,
    IvaBreakdownDto Iva,
    IReadOnlyList<ComponentDto> Components);
