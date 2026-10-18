using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Domain;

/// <summary>
/// The order left the sellers: Shipping books a courier per parcel. Carries everything Shipping needs, including the
/// cash the courier must collect for cash-on-delivery orders (zero otherwise).
/// </summary>
public sealed record OrderDispatched(OrderId OrderId, long Number, string DeliveryCity, OrderDelivery Delivery, IReadOnlyList<OrderParcel> Parcels,
    string CustomerPhone, Money CashToCollect, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
