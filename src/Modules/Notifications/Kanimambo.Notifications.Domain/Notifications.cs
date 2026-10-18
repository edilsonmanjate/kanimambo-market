using System.Net.Mail;
using System.Text.RegularExpressions;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Domain;

/// <summary>How a message reaches a customer. SMS is the default: not every customer has a smartphone.</summary>
public enum ChannelKind
{
    Sms,
    WhatsApp,
    Email,
}

/// <summary>Who a message goes to: always a phone number, sometimes an e-mail address.</summary>
public sealed record Recipient(string Phone, string? Email);

/// <summary>
/// What a message says, independent of how it travels: a subject, the one-line essentials (all an SMS can carry),
/// the details richer channels show, and an optional link.
/// </summary>
public sealed record MessageContent(string Subject, string Headline, string Details, Uri? Link);

public enum DeliveryStatus
{
    Sent,
    Failed,
}

/// <summary>
/// A customer's choice of channels, in order of preference (the first that works is used). Keyed by phone number,
/// which is how customers are identified at checkout; owned by the account that registered it.
/// </summary>
public sealed partial class ContactPreferences : AggregateRoot<string>
{
    private ContactPreferences()
    {
    }

    private ContactPreferences(string phone, string ownerId, string? email, IReadOnlyList<ChannelKind> channels, DateTimeOffset now)
        : base(phone)
    {
        OwnerId = ownerId;
        Apply(email, channels, now);
    }

    public string Phone => Id;

    public string OwnerId { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public IReadOnlyList<ChannelKind> Channels { get; private set; } = [];

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<ContactPreferences> Create(string phone, string ownerId, string? email, IReadOnlyList<ChannelKind> channels, DateTimeOffset now)
    {
        if (!MozambicanPhone().IsMatch(phone ?? string.Empty))
        {
            return Error.Validation(new FieldError("phone", "Phone numbers use the 258XXXXXXXXX format."));
        }

        return Validate(email, channels) is { } error ? error : new ContactPreferences(phone!, ownerId, email?.Trim(), channels, now);
    }

    public Result Update(string? email, IReadOnlyList<ChannelKind> channels, DateTimeOffset now)
    {
        if (Validate(email, channels) is { } error)
        {
            return error;
        }

        Apply(email?.Trim(), channels, now);
        return Result.Success();
    }

    private static ValidationError? Validate(string? email, IReadOnlyList<ChannelKind> channels)
    {
        var errors = new List<FieldError>();
        if (channels is null || channels.Count == 0)
        {
            errors.Add(new FieldError("channels", "Choose at least one channel."));
        }
        else if (channels.Distinct().Count() != channels.Count)
        {
            errors.Add(new FieldError("channels", "Each channel can appear once."));
        }

        if (email is { Length: > 0 } && !MailAddress.TryCreate(email.Trim(), out _))
        {
            errors.Add(new FieldError("email", "Not a valid e-mail address."));
        }

        if (channels?.Contains(ChannelKind.Email) == true && string.IsNullOrWhiteSpace(email))
        {
            errors.Add(new FieldError("email", "E-mail notifications need an e-mail address."));
        }

        return errors.Count == 0 ? null : Error.Validation(errors);
    }

    private void Apply(string? email, IReadOnlyList<ChannelKind> channels, DateTimeOffset now)
    {
        Email = string.IsNullOrWhiteSpace(email) ? null : email;
        Channels = [.. channels];
        UpdatedAt = now;
    }

    [GeneratedRegex(@"^258\d{9}$")]
    private static partial Regex MozambicanPhone();
}

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
