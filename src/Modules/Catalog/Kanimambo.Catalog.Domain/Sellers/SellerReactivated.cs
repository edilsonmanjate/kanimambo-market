using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Domain.Sellers;

public sealed record SellerReactivated(SellerId SellerId, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
