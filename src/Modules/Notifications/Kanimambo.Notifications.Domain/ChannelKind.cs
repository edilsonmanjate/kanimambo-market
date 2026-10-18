namespace Kanimambo.Notifications.Domain;

/// <summary>How a message reaches a customer. SMS is the default: not every customer has a smartphone.</summary>
public enum ChannelKind
{
    Sms,
    WhatsApp,
    Email,
}
