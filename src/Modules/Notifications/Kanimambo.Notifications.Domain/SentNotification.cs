using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Notifications.Domain;

/// <summary>One delivery attempt, as sent: kept for support ("did the customer get the SMS?") and for audits.</summary>
public sealed class SentNotification : AggregateRoot<Guid>
{
    private SentNotification()
    {
    }

    private SentNotification(Guid eventId, string messageType, ChannelKind channel, string recipient, long? orderNumber, DeliveryStatus status,
        string? text, string? providerMessageId, string? error, DateTimeOffset at)
        : base(Guid.NewGuid())
    {
        EventId = eventId;
        MessageType = messageType;
        Channel = channel;
        Recipient = recipient;
        OrderNumber = orderNumber;
        Status = status;
        Text = text;
        ProviderMessageId = providerMessageId;
        Error = error;
        At = at;
    }

    /// <summary>The integration event that triggered the message.</summary>
    public Guid EventId { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public ChannelKind Channel { get; private set; }

    /// <summary>Phone number or e-mail address the message went to.</summary>
    public string Recipient { get; private set; } = string.Empty;

    public long? OrderNumber { get; private set; }

    public DeliveryStatus Status { get; private set; }

    /// <summary>The text exactly as the channel rendered it (the SMS after GSM-7 conversion and shortening).</summary>
    public string? Text { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset At { get; private set; }

    public static SentNotification Sent(Guid eventId, string messageType, ChannelKind channel, string recipient, long? orderNumber, string text,
        string providerMessageId, DateTimeOffset at) =>
        new(eventId, messageType, channel, recipient, orderNumber, DeliveryStatus.Sent, text, providerMessageId, null, at);

    public static SentNotification Failed(Guid eventId, string messageType, ChannelKind channel, string recipient, long? orderNumber, string error,
        DateTimeOffset at) =>
        new(eventId, messageType, channel, recipient, orderNumber, DeliveryStatus.Failed, null, null, error, at);
}
