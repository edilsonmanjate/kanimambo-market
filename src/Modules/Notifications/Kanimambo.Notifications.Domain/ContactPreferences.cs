using System.Net.Mail;
using System.Text.RegularExpressions;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Domain;

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
