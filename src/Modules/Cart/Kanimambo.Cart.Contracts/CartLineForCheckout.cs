namespace Kanimambo.Cart.Contracts;

public sealed record CartLineForCheckout(Guid ProductId, int Quantity);
