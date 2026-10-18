namespace Kanimambo.Ordering.Domain;

public sealed record OrderStatusChange(OrderStatus From, OrderStatus To, DateTimeOffset At, string? Reason);
