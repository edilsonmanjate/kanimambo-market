using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

/// <summary>Observer: a refunded order gets credit notes.</summary>
public sealed class CreditWhenOrderRefunded(ISender sender) : INotificationHandler<OrderStatusChangedIntegrationEvent>
{
    public async ValueTask Handle(OrderStatusChangedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.To != "Refunded")
        {
            return;
        }

        var credited = await sender.Send(new CreditRefundedOrder(notification.EventId, notification.OrderId, notification.Reason ?? "Encomenda reembolsada"), cancellationToken);
        if (credited.IsFailure)
        {
            throw new InvalidOperationException($"Could not credit order {notification.OrderNumber}: {credited.Error.Message}");
        }
    }
}
