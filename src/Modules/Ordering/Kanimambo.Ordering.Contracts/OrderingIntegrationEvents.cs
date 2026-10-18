using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Contracts;

/// <summary>Where an order came from: a shopping cart or a B2B draft (reorder).</summary>
public static class CheckoutSources
{
    public const string Cart = "cart";
    public const string Draft = "draft";
}

public sealed record OrderLineContract(Guid ProductId, Guid SellerId, string Sku, string Name, int Quantity, decimal LineTotalMzn);

/// <summary>An order was accepted. The Cart module closes the cart it came from.</summary>
public sealed record OrderPlacedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    long OrderNumber,
    string CheckoutSource,
    Guid CheckoutSourceId,
    string CustomerId,
    decimal TotalMzn) : IIntegrationEvent;

/// <summary>
/// An order was paid (mobile money / card confirmation, or cash collected on delivery). Subscribers:
/// stock reservation (Catalog) and invoices (Invoicing: line totals, discount and delivery fee are all here, so
/// invoices add up to <see cref="TotalMzn"/>); SMS to the customer and the seller (Notifications, Phase 7) will
/// subscribe to the same event without Ordering knowing about them.
/// </summary>
public sealed record OrderPaidIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    long OrderNumber,
    string CustomerId,
    string CustomerPhone,
    string PaymentMethod,
    decimal TotalMzn,
    IReadOnlyList<OrderLineContract> Lines,
    string CustomerType,
    string CustomerNuit,
    decimal SubtotalMzn,
    decimal DiscountMzn,
    decimal ShippingFeeMzn,
    string DeliveryMethod) : IIntegrationEvent;

/// <summary>Every lifecycle change, for tracking and customer notifications.</summary>
public sealed record OrderStatusChangedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    long OrderNumber,
    string CustomerPhone,
    string From,
    string To,
    string? Reason) : IIntegrationEvent;

/// <summary>
/// The order now waits for a prepaid payment (first request or a retry). The Payments module subscribes and
/// starts the mobile-money / card payment; <see cref="Attempt"/> distinguishes retries.
/// </summary>
public sealed record OrderPaymentRequestedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    long OrderNumber,
    int Attempt,
    string PaymentMethod,
    string CustomerPhone,
    decimal AmountMzn) : IIntegrationEvent;

public sealed record DispatchedParcelContract(Guid SellerId, string OriginCity, decimal WeightKg);

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
