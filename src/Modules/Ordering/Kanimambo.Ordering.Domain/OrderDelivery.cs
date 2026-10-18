namespace Kanimambo.Ordering.Domain;

/// <summary>How the customer chose to receive the order, as quoted by Shipping at checkout.</summary>
public sealed record OrderDelivery(string Method, string? District, string? PickupPointId, DateOnly EstimatedDelivery);
