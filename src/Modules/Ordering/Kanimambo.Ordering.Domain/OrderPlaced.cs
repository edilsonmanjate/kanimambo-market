using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Domain;

/// <summary>Domain events carry what their subscribers need, so they can be published without reloading the order.</summary>
public sealed record OrderPlaced(OrderId OrderId, long Number, string CheckoutSource, Guid CheckoutSourceId, string CustomerId, Money Total, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt);
