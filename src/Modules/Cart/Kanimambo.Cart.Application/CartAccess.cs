using Kanimambo.Cart.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Application;

internal static class CartAccess
{
    /// <summary>Loads a cart the current actor may use. A cart that belongs to someone else is reported as not found.</summary>
    public static async Task<Result<ShoppingCart>> LoadAsync(ICartRepository carts, ICurrentActor actor, Guid cartId, CancellationToken cancellationToken)
    {
        var cart = await carts.GetAsync(new CartId(cartId), cancellationToken);
        return cart is not null && cart.Customer.CanAccess(actor)
            ? cart
            : Error.NotFound("cart.not_found", $"Cart {cartId} does not exist.");
    }
}
