namespace Kanimambo.Ordering.Contracts;

public sealed record OrderLineContract(Guid ProductId, Guid SellerId, string Sku, string Name, int Quantity, decimal LineTotalMzn);
