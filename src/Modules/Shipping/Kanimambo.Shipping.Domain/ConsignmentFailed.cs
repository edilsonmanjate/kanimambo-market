using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Domain;

public sealed record ConsignmentFailed(Guid ConsignmentId, Guid OrderId, long OrderNumber, Guid ParcelId, string Reason, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt);
