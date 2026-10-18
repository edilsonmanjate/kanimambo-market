using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Shipping.Contracts;

/// <summary>A parcel moved forward: booked with a courier, picked up, in transit, ready for collection, delivered, failed.</summary>
public sealed record ParcelStatusChangedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ConsignmentId,
    Guid OrderId,
    long OrderNumber,
    Guid ParcelId,
    string Status,
    string Courier,
    string? TrackingCode,
    string RecipientPhone,
    string? Detail) : IIntegrationEvent;
