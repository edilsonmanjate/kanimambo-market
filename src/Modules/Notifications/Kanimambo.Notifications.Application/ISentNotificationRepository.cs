using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application;

public interface ISentNotificationRepository
{
    void Add(SentNotification notification);

    Task<IReadOnlyList<SentNotification>> ListAsync(string? recipient, long? orderNumber, CancellationToken cancellationToken);
}
