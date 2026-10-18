using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Payments.Application;

/// <summary>Observer: Payments starts working when Ordering says an order awaits payment.</summary>
public sealed class StartPaymentWhenRequested(ISender sender) : INotificationHandler<OrderPaymentRequestedIntegrationEvent>
{
    public async ValueTask Handle(OrderPaymentRequestedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var started = await sender.Send(new StartPayment(notification.EventId, notification.OrderId, notification.OrderNumber,
            notification.Attempt, notification.PaymentMethod, notification.CustomerPhone, notification.AmountMzn), cancellationToken);

        if (started.IsFailure)
        {
            throw new InvalidOperationException($"Could not start the payment of order {notification.OrderNumber}: {started.Error.Message}");
        }
    }
}
