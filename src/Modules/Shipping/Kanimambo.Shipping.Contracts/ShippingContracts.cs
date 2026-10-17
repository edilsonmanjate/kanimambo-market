using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Contracts;

/// <summary>One seller's part of an order: it leaves from the seller's city and weighs what its products weigh.</summary>
public sealed record ParcelRequest(Guid SellerId, string OriginCity, decimal WeightKg);

/// <summary>
/// What to deliver and where. <see cref="Method"/> is a shipping method name (<c>SameDay</c>, <c>Courier</c>,
/// <c>Pickup</c>…); when it is null the configured preference order picks the first method that can serve.
/// </summary>
public sealed record ShippingQuoteRequest(
    string DestinationCity,
    string? District,
    string? Method,
    string? PickupPointId,
    IReadOnlyList<ParcelRequest> Parcels);

/// <summary>How one parcel travels: its fee, the courier that carries it and, for same-day, the vehicle.</summary>
public sealed record ParcelQuote(Guid SellerId, Money Fee, string Courier, string? Vehicle, DateOnly EstimatedDelivery);

/// <summary>A priced delivery option. <see cref="PickupPointId"/> is set when the parcels go to a pickup point.</summary>
public sealed record ShippingQuote(string Method, string Description, Money Fee, DateOnly EstimatedDelivery, string? PickupPointId, IReadOnlyList<ParcelQuote> Parcels);

/// <summary>Delivery prices for checkout (synchronous, no side effects).</summary>
public interface IShippingQuoter
{
    /// <summary>The quote for the requested method, or for the first configured method that can serve the order.</summary>
    Task<Result<ShippingQuote>> QuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken);

    /// <summary>Every method that can serve the order, cheapest first (the method in the request is ignored).</summary>
    Task<IReadOnlyList<ShippingQuote>> OptionsAsync(ShippingQuoteRequest request, CancellationToken cancellationToken);
}

/// <summary>A parcel moved forward: booked with a courier, picked up, in transit, ready for collection, delivered, failed.</summary>
public sealed record ParcelStatusChangedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ConsignmentId,
    Guid OrderId,
    long OrderNumber,
    Guid ParcelId,
    string Status,
    string Courier,
    string? TrackingCode,
    string RecipientPhone,
    string? Detail) : IIntegrationEvent;

/// <summary>Every parcel of the order reached the customer. Ordering marks the order delivered (and cash orders paid).</summary>
public sealed record OrderDeliveredIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ConsignmentId,
    Guid OrderId,
    long OrderNumber) : IIntegrationEvent;

/// <summary>A parcel could not be booked or delivered; the back-office and the customer need to act.</summary>
public sealed record DeliveryFailedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ConsignmentId,
    Guid OrderId,
    long OrderNumber,
    Guid ParcelId,
    string Reason) : IIntegrationEvent;
