using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Domain.Sellers;

public sealed record SellerSuspended(SellerId SellerId, string Reason, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
