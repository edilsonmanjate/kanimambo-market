using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Shipping.Contracts;

/// <summary>A parcel could not be booked or delivered; the back-office and the customer need to act.</summary>
public sealed record DeliveryFailedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ConsignmentId,
    Guid OrderId,
    long OrderNumber,
    Guid ParcelId,
    string Reason) : IIntegrationEvent;
