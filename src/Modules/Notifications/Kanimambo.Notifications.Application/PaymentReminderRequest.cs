using Kanimambo.Notifications.Application.Messages;

namespace Kanimambo.Notifications.Application;

public sealed record PaymentReminderRequest(long Number, string Reason) : MessageRequest(Number)
{
    public override CustomerNotification For(INotificationChannel channel, NotificationSettings settings) =>
        new PaymentReminder(channel, Number, Reason, settings.OrderLink(Number));
}
