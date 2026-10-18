using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.Shipping.Contracts;

namespace Kanimambo.Shipping.Application;

/// <summary>Observer: Shipping starts working when Ordering says the sellers handed an order over.</summary>
public sealed class ShipOrderWhenDispatched(ISender sender) : INotificationHandler<OrderDispatchedIntegrationEvent>
{
    public async ValueTask Handle(OrderDispatchedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var shipped = await sender.Send(new ShipOrder(notification.EventId, notification.OrderId, notification.OrderNumber, notification.DeliveryMethod,
            notification.DeliveryCity, notification.DeliveryDistrict, notification.PickupPointId, notification.CustomerPhone, notification.CashToCollectMzn,
            [.. notification.Parcels.Select(p => new ParcelRequest(p.SellerId, p.OriginCity, p.WeightKg))]), cancellationToken);

        if (shipped.IsFailure)
        {
            throw new InvalidOperationException($"Could not ship order {notification.OrderNumber}: {shipped.Error.Message}");
        }
    }
}
