using Kanimambo.Notifications.Application.Messages;

namespace Kanimambo.Notifications.Application;

public sealed record DeliveryUpdateRequest(long Number, string Status, string Courier, string? TrackingCode) : MessageRequest(Number)
{
    public override CustomerNotification For(INotificationChannel channel, NotificationSettings settings) =>
        new DeliveryUpdate(channel, Number, Status, Courier, TrackingCode, settings.OrderLink(Number));
}
