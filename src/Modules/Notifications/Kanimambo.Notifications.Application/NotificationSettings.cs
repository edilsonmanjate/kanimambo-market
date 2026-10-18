using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application;

/// <summary>Where customer-facing links point, and the channels used when a customer set no preference (SMS).</summary>
public sealed record NotificationSettings(Uri PublicBaseAddress, IReadOnlyList<ChannelKind> DefaultChannels)
{
    public Uri OrderLink(long orderNumber) => new(PublicBaseAddress, $"e/KM-{orderNumber}");

    public Uri DocumentLink(Guid documentId) => new(PublicBaseAddress, $"f/{documentId:N}");
}
