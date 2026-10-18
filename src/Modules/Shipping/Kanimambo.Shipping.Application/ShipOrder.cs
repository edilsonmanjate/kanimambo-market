using Kanimambo.SharedKernel.Messaging;
using Kanimambo.Shipping.Contracts;

namespace Kanimambo.Shipping.Application;

/// <summary>Book a courier for every parcel of a dispatched order. Idempotent: one consignment per order.</summary>
public sealed record ShipOrder(
    Guid IdempotencyKey,
    Guid OrderId,
    long OrderNumber,
    string Method,
    string DestinationCity,
    string? District,
    string? PickupPointId,
    string RecipientPhone,
    decimal CashToCollectMzn,
    IReadOnlyList<ParcelRequest> Parcels) : IIdempotentCommand;
