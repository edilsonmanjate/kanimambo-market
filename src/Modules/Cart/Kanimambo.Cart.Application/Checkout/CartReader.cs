using Kanimambo.Cart.Contracts;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Application.Checkout;

/// <summary>Implements the public contract checkout uses to read a cart (with the same access rule as the API).</summary>
internal sealed class CartReader(ICartRepository carts, ICurrentActor actor) : ICartReader
{
    public async Task<Result<CartForCheckout>> GetForCheckoutAsync(Guid cartId, CancellationToken cancellationToken)
    {
        var cart = await CartAccess.LoadAsync(carts, actor, cartId, cancellationToken);
        if (cart.IsFailure)
        {
            return cart.Error;
        }

        var c = cart.Value;
        return new CartForCheckout(c.Id.Value, c.Version, c.Customer.Id, c.Customer.Type, c.DeliveryCity,
            [.. c.Lines.Select(l => new CartLineForCheckout(l.ProductId, l.Quantity))], c.CheckedOutOrderId);
    }
}
