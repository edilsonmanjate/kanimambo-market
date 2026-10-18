namespace Kanimambo.Catalog.Application.Products;

public sealed record IvaBreakdownDto(IReadOnlyList<IvaLineDto> Lines, MoneyDto Net, MoneyDto Iva, MoneyDto Gross);
