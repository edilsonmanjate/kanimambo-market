using Kanimambo.Cart.Contracts;
using Kanimambo.Cart.Domain;
using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
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

/// <summary>Closes the cart once its order exists. Idempotent: the event may be delivered more than once.</summary>
public sealed record CloseCheckedOutCart(Guid IdempotencyKey, Guid CartId, Guid OrderId) : IIdempotentCommand;

/// <summary>Observer: the Cart module reacts to Ordering's event; Ordering never writes to the cart schema.</summary>
public sealed class CloseCartWhenOrderIsPlaced(ISender sender) : INotificationHandler<OrderPlacedIntegrationEvent>
{
    public async ValueTask Handle(OrderPlacedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.CheckoutSource != CheckoutSources.Cart)
        {
            return;
        }

        var result = await sender.Send(new CloseCheckedOutCart(notification.EventId, notification.CheckoutSourceId, notification.OrderId), cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Could not close cart {notification.CheckoutSourceId}: {result.Error.Message}");
        }
    }
}

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
