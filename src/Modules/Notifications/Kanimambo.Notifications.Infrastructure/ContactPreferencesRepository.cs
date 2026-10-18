using Kanimambo.Notifications.Application;
using Kanimambo.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Notifications.Infrastructure;

internal sealed class ContactPreferencesRepository(NotificationsDbContext db) : IContactPreferencesRepository
{
    public void Add(ContactPreferences preferences) => db.Preferences.Add(preferences);

    public Task<ContactPreferences?> GetAsync(string phone, CancellationToken cancellationToken) =>
        db.Preferences.SingleOrDefaultAsync(p => p.Id == phone, cancellationToken);
}
