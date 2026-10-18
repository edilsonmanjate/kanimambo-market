namespace Kanimambo.Notifications.Application;

public sealed record SentNotificationDto(Guid Id, string MessageType, string Channel, string Recipient, long? OrderNumber, string Status, string? Text,
    string? ProviderMessageId, string? Error, DateTimeOffset At);
