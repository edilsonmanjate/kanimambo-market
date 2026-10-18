namespace Kanimambo.Catalog.Application.Products;

public sealed record IvaLineDto(string Category, decimal Rate, MoneyDto Net, MoneyDto Iva);
