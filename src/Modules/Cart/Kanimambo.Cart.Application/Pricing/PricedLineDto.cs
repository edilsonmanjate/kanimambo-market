namespace Kanimambo.Cart.Application.Pricing;

public sealed record PricedLineDto(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice, string Currency, decimal ExchangeRate, decimal LineTotalMzn, string Availability);
