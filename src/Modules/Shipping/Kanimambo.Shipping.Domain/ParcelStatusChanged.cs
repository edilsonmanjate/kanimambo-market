using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Domain;

public sealed record ParcelStatusChanged(
    Guid ConsignmentId, Guid OrderId, long OrderNumber, Guid ParcelId, ParcelStatus Status, string Courier, string? TrackingCode,
    string RecipientPhone, string? Detail, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
