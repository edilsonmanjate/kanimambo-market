using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Shipping.Contracts;

/// <summary>Every parcel of the order reached the customer. Ordering marks the order delivered (and cash orders paid).</summary>
public sealed record OrderDeliveredIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ConsignmentId,
    Guid OrderId,
    long OrderNumber) : IIntegrationEvent;
