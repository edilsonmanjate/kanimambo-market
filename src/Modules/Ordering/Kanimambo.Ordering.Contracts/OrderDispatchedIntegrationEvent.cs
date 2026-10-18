using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Contracts;

/// <summary>
/// The sellers handed the order over for delivery. The Shipping module subscribes and books a courier for every
/// parcel. <see cref="CashToCollectMzn"/> is what the courier collects for cash-on-delivery orders (0 otherwise).
/// </summary>
public sealed record OrderDispatchedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    long OrderNumber,
    string DeliveryMethod,
    string DeliveryCity,
    string? DeliveryDistrict,
    string? PickupPointId,
    string CustomerPhone,
    decimal CashToCollectMzn,
    IReadOnlyList<DispatchedParcelContract> Parcels) : IIntegrationEvent;
