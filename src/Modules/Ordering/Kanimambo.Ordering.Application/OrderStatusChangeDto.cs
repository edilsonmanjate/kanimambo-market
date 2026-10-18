namespace Kanimambo.Ordering.Application;

public sealed record OrderStatusChangeDto(string From, string To, DateTimeOffset At, string? Reason);
