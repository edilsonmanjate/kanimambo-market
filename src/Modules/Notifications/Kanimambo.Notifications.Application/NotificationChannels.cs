using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application;

/// <summary>The registered channels by kind.</summary>
public sealed class NotificationChannels(IEnumerable<INotificationChannel> channels)
{
    private readonly Dictionary<ChannelKind, INotificationChannel> _byKind = channels.ToDictionary(c => c.Kind);

    public INotificationChannel? For(ChannelKind kind) => _byKind.GetValueOrDefault(kind);
}
