namespace Kanimambo.Cart.Contracts;

/// <summary>What checkout needs from a cart: who, where, what — prices are re-read from the catalog at checkout.</summary>
public sealed record CartForCheckout(
    Guid CartId,
    int Version,
    string CustomerId,
    string CustomerType,
    string DeliveryCity,
    IReadOnlyList<CartLineForCheckout> Lines,
    Guid? CheckedOutOrderId);
