using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Domain;

public sealed record OrderPaid(OrderId OrderId, long Number, string PaymentReference, OrderCustomer Customer, PaymentMethod PaymentMethod, Money Total,
    IReadOnlyList<OrderLine> Lines, OrderTotals Totals, string DeliveryMethod, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
