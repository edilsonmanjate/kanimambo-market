using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application;

public interface IContactPreferencesRepository
{
    void Add(ContactPreferences preferences);

    Task<ContactPreferences?> GetAsync(string phone, CancellationToken cancellationToken);
}
