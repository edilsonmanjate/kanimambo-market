using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Domain;

public sealed record ConsignmentDelivered(Guid ConsignmentId, Guid OrderId, long OrderNumber, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
