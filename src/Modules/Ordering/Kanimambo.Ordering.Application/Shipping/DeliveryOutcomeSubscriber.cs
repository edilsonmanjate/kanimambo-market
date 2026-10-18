using Kanimambo.SharedKernel.Messaging;
using Kanimambo.Shipping.Contracts;

namespace Kanimambo.Ordering.Application.Shipping;

/// <summary>Observer: when Shipping reports every parcel delivered, the order is delivered (and a cash order paid).</summary>
public sealed class DeliveryOutcomeSubscriber(ISender sender) : INotificationHandler<OrderDeliveredIntegrationEvent>
{
    public async ValueTask Handle(OrderDeliveredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var applied = await sender.Send(new ApplyDelivery(notification.EventId, notification.OrderId), cancellationToken);
        if (applied.IsFailure)
        {
            throw new InvalidOperationException($"Could not apply the delivery of order {notification.OrderNumber}: {applied.Error.Message}");
        }
    }
}
