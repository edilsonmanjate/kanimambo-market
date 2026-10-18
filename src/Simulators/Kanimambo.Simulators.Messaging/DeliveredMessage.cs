namespace Kanimambo.Simulators.Messaging;

public sealed record DeliveredMessage(string Id, string Channel, string To, string Text, string? Subject, DateTimeOffset At);
