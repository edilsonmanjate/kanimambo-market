using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application.Checkout;

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
