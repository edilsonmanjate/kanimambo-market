using Kanimambo.Notifications.Application;
using Kanimambo.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Notifications.Infrastructure;

internal sealed class SentNotificationRepository(NotificationsDbContext db) : ISentNotificationRepository
{
    public void Add(SentNotification notification) => db.Sent.Add(notification);

    public async Task<IReadOnlyList<SentNotification>> ListAsync(string? recipient, long? orderNumber, CancellationToken cancellationToken) =>
        await db.Sent.AsNoTracking()
            .Where(n => (recipient == null || n.Recipient == recipient) && (orderNumber == null || n.OrderNumber == orderNumber))
            .OrderBy(n => n.At)
            .Take(500)
            .ToListAsync(cancellationToken);
}
