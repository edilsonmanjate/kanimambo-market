using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Domain;

public sealed record OrderStatusChanged(OrderId OrderId, long Number, string CustomerPhone, OrderStatus From, OrderStatus To, string? Reason, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt);
