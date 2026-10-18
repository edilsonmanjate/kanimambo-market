using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Contracts;

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
