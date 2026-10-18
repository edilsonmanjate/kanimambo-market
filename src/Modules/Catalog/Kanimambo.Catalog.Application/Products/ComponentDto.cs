namespace Kanimambo.Catalog.Application.Products;

public sealed record ComponentDto(Guid ProductId, string Sku, string Name, string Kind, int Quantity, IReadOnlyList<ComponentDto> Components);
