using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Notifications.Application;

public sealed record ListSentNotifications(string? Recipient, long? OrderNumber) : IQuery<IReadOnlyList<SentNotificationDto>>;
