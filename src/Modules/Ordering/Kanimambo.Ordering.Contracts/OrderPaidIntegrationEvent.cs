using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Contracts;

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
