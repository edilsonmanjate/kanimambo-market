namespace Kanimambo.Ordering.Application;

public sealed record OrderLineDto(Guid ProductId, Guid SellerId, string Sku, string Name, int Quantity, decimal UnitPrice, decimal LineTotal);
