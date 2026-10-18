using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application;

/// <summary>
/// The data of a message, independent of any channel. <see cref="For"/> pairs it with a channel at send time — the
/// moment the Bridge is assembled, from the customer's preferences.
/// </summary>
public abstract record MessageRequest(long? OrderNumber)
{
    private static readonly IReadOnlySet<ChannelKind> AnyChannel = new HashSet<ChannelKind>(Enum.GetValues<ChannelKind>());

    /// <summary>Channels this kind of message may use (all by default).</summary>
    public virtual IReadOnlySet<ChannelKind> AllowedChannels => AnyChannel;

    public abstract CustomerNotification For(INotificationChannel channel, NotificationSettings settings);
}
