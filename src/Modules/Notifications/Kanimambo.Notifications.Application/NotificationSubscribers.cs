using Kanimambo.Invoicing.Contracts;
using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.Shipping.Contracts;

namespace Kanimambo.Notifications.Application;

/// <summary>Observer: each business event the customer should hear about becomes one message.</summary>
public sealed class NotificationSubscribers(ISender sender) :
    INotificationHandler<OrderPaidIntegrationEvent>,
    INotificationHandler<OrderStatusChangedIntegrationEvent>,
    INotificationHandler<ParcelStatusChangedIntegrationEvent>,
    INotificationHandler<FiscalDocumentIssuedIntegrationEvent>
{
    public ValueTask Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return Send(notification.EventId, notification.CustomerPhone,
            new OrderConfirmationRequest(notification.OrderNumber, notification.TotalMzn, notification.DeliveryMethod), cancellationToken);
    }

    public ValueTask Handle(OrderStatusChangedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return notification.To == "PaymentFailed"
            ? Send(notification.EventId, notification.CustomerPhone, new PaymentReminderRequest(notification.OrderNumber, notification.Reason ?? string.Empty), cancellationToken)
            : ValueTask.CompletedTask;
    }

    public ValueTask Handle(ParcelStatusChangedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return DeliveryUpdate.Notified.ContainsKey(notification.Status)
            ? Send(notification.EventId, notification.RecipientPhone,
                new DeliveryUpdateRequest(notification.OrderNumber, notification.Status, notification.Courier, notification.TrackingCode), cancellationToken)
            : ValueTask.CompletedTask;
    }

    public ValueTask Handle(FiscalDocumentIssuedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return Send(notification.EventId, notification.CustomerPhone, new InvoiceAvailableRequest(notification.OrderNumber, notification.DocumentId,
            notification.DocumentType, notification.Number, notification.IssuerName, notification.TotalMzn), cancellationToken);
    }

    private async ValueTask Send(Guid eventId, string phone, MessageRequest message, CancellationToken cancellationToken)
    {
        var sent = await sender.Send(new Notify(eventId, phone, message), cancellationToken);
        if (sent.IsFailure)
        {
            throw new InvalidOperationException($"Could not notify {phone}: {sent.Error.Message}");
        }
    }
}
