using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Contracts;

public sealed record CartLineForCheckout(Guid ProductId, int Quantity);

/// <summary>What checkout needs from a cart: who, where, what — prices are re-read from the catalog at checkout.</summary>
public sealed record CartForCheckout(
    Guid CartId,
    int Version,
    string CustomerId,
    string CustomerType,
    string DeliveryCity,
    IReadOnlyList<CartLineForCheckout> Lines,
    Guid? CheckedOutOrderId);

public interface ICartReader
{
    /// <summary>The cart, if the current actor may use it (same access rule as the Cart API).</summary>
    Task<Result<CartForCheckout>> GetForCheckoutAsync(Guid cartId, CancellationToken cancellationToken);
}
