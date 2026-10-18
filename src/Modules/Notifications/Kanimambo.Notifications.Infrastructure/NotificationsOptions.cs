using Kanimambo.Notifications.Domain;
using Kanimambo.Notifications.Infrastructure.Channels;

namespace Kanimambo.Notifications.Infrastructure;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Base of the links in messages (order tracking, invoice download).</summary>
    public Uri PublicBaseAddress { get; set; } = new("https://kanimambo.co.mz/");

    /// <summary>Channels for customers without preferences: SMS reaches every phone.</summary>
    public List<ChannelKind> DefaultChannels { get; } = [];

    public GatewayOptions Gateway { get; set; } = new();
}
