using Kanimambo.Invoicing.Contracts;
using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Contracts;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Notifications.Application;

public static class NotificationsModule
{
    public const string Name = "Notifications";
}

/// <summary>Where customer-facing links point, and the channels used when a customer set no preference (SMS).</summary>
public sealed record NotificationSettings(Uri PublicBaseAddress, IReadOnlyList<ChannelKind> DefaultChannels)
{
    public Uri OrderLink(long orderNumber) => new(PublicBaseAddress, $"e/KM-{orderNumber}");

    public Uri DocumentLink(Guid documentId) => new(PublicBaseAddress, $"f/{documentId:N}");
}

public interface IContactPreferencesRepository
{
    void Add(ContactPreferences preferences);

    Task<ContactPreferences?> GetAsync(string phone, CancellationToken cancellationToken);
}

public interface ISentNotificationRepository
{
    void Add(SentNotification notification);

    Task<IReadOnlyList<SentNotification>> ListAsync(string? recipient, long? orderNumber, CancellationToken cancellationToken);
}

/// <summary>The registered channels by kind.</summary>
public sealed class NotificationChannels(IEnumerable<INotificationChannel> channels)
{
    private readonly Dictionary<ChannelKind, INotificationChannel> _byKind = channels.ToDictionary(c => c.Kind);

    public INotificationChannel? For(ChannelKind kind) => _byKind.GetValueOrDefault(kind);
}

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

public sealed record OrderConfirmationRequest(long Number, decimal Total, string DeliveryMethod) : MessageRequest(Number)
{
    public override CustomerNotification For(INotificationChannel channel, NotificationSettings settings) =>
        new OrderConfirmation(channel, Number, Total, DeliveryMethod, settings.OrderLink(Number));
}

public sealed record PaymentReminderRequest(long Number, string Reason) : MessageRequest(Number)
{
    public override CustomerNotification For(INotificationChannel channel, NotificationSettings settings) =>
        new PaymentReminder(channel, Number, Reason, settings.OrderLink(Number));
}

public sealed record DeliveryUpdateRequest(long Number, string Status, string Courier, string? TrackingCode) : MessageRequest(Number)
{
    public override CustomerNotification For(INotificationChannel channel, NotificationSettings settings) =>
        new DeliveryUpdate(channel, Number, Status, Courier, TrackingCode, settings.OrderLink(Number));
}

public sealed record InvoiceAvailableRequest(long Number, Guid DocumentId, string DocumentType, string DocumentNumber, string IssuerName, decimal Total)
    : MessageRequest(Number)
{
    private static readonly IReadOnlySet<ChannelKind> WithLinks = new HashSet<ChannelKind> { ChannelKind.Email, ChannelKind.WhatsApp };

    /// <summary>Several documents per order would flood an SMS inbox, and a PDF link needs a smartphone or a computer.</summary>
    public override IReadOnlySet<ChannelKind> AllowedChannels => WithLinks;

    public override CustomerNotification For(INotificationChannel channel, NotificationSettings settings) =>
        new InvoiceAvailable(channel, DocumentType == "CreditNote" ? "Nota de crédito" : "Fatura", DocumentNumber, IssuerName, Total, settings.DocumentLink(DocumentId));
}

/// <summary>Send one message to the customer behind <paramref name="Phone"/>, on the first preferred channel that works.</summary>
public sealed record Notify(Guid IdempotencyKey, string Phone, MessageRequest Message) : IIdempotentCommand;

public sealed record SetContactPreferences(string Phone, string? Email, IReadOnlyList<string> Channels) : ICommand<ContactPreferencesDto>;

public sealed record GetContactPreferences(string Phone) : IQuery<ContactPreferencesDto>;

public sealed record ListSentNotifications(string? Recipient, long? OrderNumber) : IQuery<IReadOnlyList<SentNotificationDto>>;

public sealed record ContactPreferencesDto(string Phone, string? Email, IReadOnlyList<string> Channels);

public sealed record SentNotificationDto(Guid Id, string MessageType, string Channel, string Recipient, long? OrderNumber, string Status, string? Text,
    string? ProviderMessageId, string? Error, DateTimeOffset At);

/// <summary>Observer: each business event the customer should hear about becomes one message.</summary>
public sealed class NotificationSubscribers(ISender sender) :
    INotificationHandler<OrderPaidIntegrationEvent>,
    INotificationHandler<OrderStatusChangedIntegrationEvent>,
    INotificationHandler<ParcelStatusChangedIntegrationEvent>,
    INotificationHandler<FiscalDocumentIssuedIntegrationEvent>
{
    public ValueTask Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return Send(notification.EventId, notification.CustomerPhone,
            new OrderConfirmationRequest(notification.OrderNumber, notification.TotalMzn, notification.DeliveryMethod), cancellationToken);
    }

    public ValueTask Handle(OrderStatusChangedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return notification.To == "PaymentFailed"
            ? Send(notification.EventId, notification.CustomerPhone, new PaymentReminderRequest(notification.OrderNumber, notification.Reason ?? string.Empty), cancellationToken)
            : ValueTask.CompletedTask;
    }

    public ValueTask Handle(ParcelStatusChangedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return DeliveryUpdate.Notified.ContainsKey(notification.Status)
            ? Send(notification.EventId, notification.RecipientPhone,
                new DeliveryUpdateRequest(notification.OrderNumber, notification.Status, notification.Courier, notification.TrackingCode), cancellationToken)
            : ValueTask.CompletedTask;
    }

    public ValueTask Handle(FiscalDocumentIssuedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return Send(notification.EventId, notification.CustomerPhone, new InvoiceAvailableRequest(notification.OrderNumber, notification.DocumentId,
            notification.DocumentType, notification.Number, notification.IssuerName, notification.TotalMzn), cancellationToken);
    }

    private async ValueTask Send(Guid eventId, string phone, MessageRequest message, CancellationToken cancellationToken)
    {
        var sent = await sender.Send(new Notify(eventId, phone, message), cancellationToken);
        if (sent.IsFailure)
        {
            throw new InvalidOperationException($"Could not notify {phone}: {sent.Error.Message}");
        }
    }
}

public sealed partial class NotificationHandlers(
    IContactPreferencesRepository preferences,
    ISentNotificationRepository sentNotifications,
    NotificationChannels channels,
    NotificationSettings settings,
    ICurrentActor actor,
    TimeProvider timeProvider,
    ILogger<NotificationHandlers> logger) :
    IRequestHandler<Notify, Result>,
    IRequestHandler<SetContactPreferences, Result<ContactPreferencesDto>>,
    IRequestHandler<GetContactPreferences, Result<ContactPreferencesDto>>,
    IRequestHandler<ListSentNotifications, Result<IReadOnlyList<SentNotificationDto>>>
{
    /// <summary>
    /// Tries the customer's channels in order of preference — skipping those the message may not use or that lack an
    /// address — and stops at the first that delivers. Every attempt is recorded. If only transient failures happened,
    /// the command fails so the outbox tries again later; permanent failures are recorded and not retried.
    /// </summary>
    public async ValueTask<Result> Handle(Notify request, CancellationToken cancellationToken)
    {
        var preference = await preferences.GetAsync(request.Phone, cancellationToken);
        var recipient = new Recipient(request.Phone, preference?.Email);
        var order = preference?.Channels ?? settings.DefaultChannels;
        var transientOnly = true;
        var attempted = false;

        foreach (var kind in order.Where(request.Message.AllowedChannels.Contains))
        {
            if ((kind == ChannelKind.Email && recipient.Email is null) || channels.For(kind) is not { } channel)
            {
                continue;
            }

            var notification = request.Message.For(channel, settings);
            var address = kind == ChannelKind.Email ? recipient.Email! : recipient.Phone;
            var now = timeProvider.GetUtcNow();
            var result = await notification.SendAsync(recipient, cancellationToken);
            attempted = true;

            if (result.IsSuccess)
            {
                sentNotifications.Add(SentNotification.Sent(request.IdempotencyKey, notification.Type, kind, address, request.Message.OrderNumber,
                    result.Value.RenderedText, result.Value.ProviderMessageId, now));
                return Result.Success();
            }

            transientOnly &= result.Error is ChannelError { IsTransient: true };
            sentNotifications.Add(SentNotification.Failed(request.IdempotencyKey, notification.Type, kind, address, request.Message.OrderNumber, result.Error.Message, now));
            LogFallback(logger, kind, request.Phone, result.Error.Code);
        }

        if (!attempted)
        {
            LogNoChannel(logger, request.Message.GetType().Name, request.Phone);
            return Result.Success();
        }

        return transientOnly
            ? new ChannelError(order[0], "unavailable", $"No channel could deliver to {request.Phone} right now.", transient: true)
            : Result.Success();
    }

    public async ValueTask<Result<ContactPreferencesDto>> Handle(SetContactPreferences request, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Error.Unauthorized("notifications.sign_in", "Sign in to choose how we contact you.");
        }

        var kinds = new List<ChannelKind>();
        foreach (var name in request.Channels ?? [])
        {
            if (!Enum.TryParse<ChannelKind>(name, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            {
                return Error.Validation(new FieldError("channels", $"Unknown channel '{name}'. Use Sms, WhatsApp or Email."));
            }

            kinds.Add(kind);
        }

        var now = timeProvider.GetUtcNow();
        var existing = await preferences.GetAsync(request.Phone, cancellationToken);
        if (existing is null)
        {
            var created = ContactPreferences.Create(request.Phone, actor.Name, request.Email, kinds, now);
            if (created.IsFailure)
            {
                return created.Error;
            }

            preferences.Add(created.Value);
            return ToDto(created.Value);
        }

        if (existing.OwnerId != actor.Name && !actor.IsInRole(KnownRoles.Backoffice))
        {
            return Error.Forbidden("notifications.not_your_number", "These preferences belong to another account.");
        }

        var updated = existing.Update(request.Email, kinds, now);
        return updated.IsSuccess ? ToDto(existing) : updated.Error;
    }

    public async ValueTask<Result<ContactPreferencesDto>> Handle(GetContactPreferences request, CancellationToken cancellationToken)
    {
        var existing = await preferences.GetAsync(request.Phone, cancellationToken);
        if (existing is null || (existing.OwnerId != actor.Name && !actor.IsInRole(KnownRoles.Backoffice)))
        {
            return new ContactPreferencesDto(request.Phone, null, [.. settings.DefaultChannels.Select(c => c.ToString())]);
        }

        return ToDto(existing);
    }

    public async ValueTask<Result<IReadOnlyList<SentNotificationDto>>> Handle(ListSentNotifications request, CancellationToken cancellationToken) =>
        (await sentNotifications.ListAsync(request.Recipient, request.OrderNumber, cancellationToken))
            .Select(n => new SentNotificationDto(n.Id, n.MessageType, n.Channel.ToString(), n.Recipient, n.OrderNumber, n.Status.ToString(), n.Text,
                n.ProviderMessageId, n.Error, n.At))
            .ToList();

    private static ContactPreferencesDto ToDto(ContactPreferences p) => new(p.Phone, p.Email, [.. p.Channels.Select(c => c.ToString())]);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Channel} could not deliver to {Phone} ({Code}); trying the next channel")]
    private static partial void LogFallback(ILogger logger, ChannelKind channel, string phone, string code);

    [LoggerMessage(Level = LogLevel.Information, Message = "No allowed channel for {Message} to {Phone}; nothing sent")]
    private static partial void LogNoChannel(ILogger logger, string message, string phone);
}
