using Kanimambo.Notifications.Application.Messages;

namespace Kanimambo.Notifications.Application;

public sealed record OrderConfirmationRequest(long Number, decimal Total, string DeliveryMethod) : MessageRequest(Number)
{
    public override CustomerNotification For(INotificationChannel channel, NotificationSettings settings) =>
        new OrderConfirmation(channel, Number, Total, DeliveryMethod, settings.OrderLink(Number));
}
