using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Notifications.Application;

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
