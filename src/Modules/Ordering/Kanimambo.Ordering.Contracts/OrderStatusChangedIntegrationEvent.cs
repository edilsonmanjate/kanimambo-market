using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Contracts;

/// <summary>Every lifecycle change, for tracking and customer notifications.</summary>
public sealed record OrderStatusChangedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    long OrderNumber,
    string CustomerPhone,
    string From,
    string To,
    string? Reason) : IIntegrationEvent;
