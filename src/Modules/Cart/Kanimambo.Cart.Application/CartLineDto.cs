namespace Kanimambo.Cart.Application;

public sealed record CartLineDto(Guid ProductId, string Sku, string Name, int Quantity);
