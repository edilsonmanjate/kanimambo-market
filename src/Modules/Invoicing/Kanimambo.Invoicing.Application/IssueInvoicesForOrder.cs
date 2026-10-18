using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

/// <summary>Issue the invoices of a paid order: one per seller (goods) plus the marketplace's delivery invoice.</summary>
public sealed record IssueInvoicesForOrder(
    Guid IdempotencyKey,
    Guid OrderId,
    long OrderNumber,
    string CustomerId,
    string CustomerType,
    string CustomerNuit,
    string CustomerPhone,
    decimal DiscountMzn,
    decimal ShippingFeeMzn,
    string DeliveryMethod,
    IReadOnlyList<OrderLineContract> Lines) : IIdempotentCommand;
