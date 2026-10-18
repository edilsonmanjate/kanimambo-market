using Kanimambo.Cart.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Application.Checkout;

public sealed class CloseCheckedOutCartHandler(ICartRepository carts, TimeProvider timeProvider) : IRequestHandler<CloseCheckedOutCart, Result>
{
    public async ValueTask<Result> Handle(CloseCheckedOutCart request, CancellationToken cancellationToken)
    {
        var cart = await carts.GetAsync(new CartId(request.CartId), cancellationToken);
        return cart is null
            ? Result.Success() // nothing to close (e.g. expired cart)
            : cart.MarkCheckedOut(request.OrderId, timeProvider.GetUtcNow());
    }
}
