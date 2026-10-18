namespace Kanimambo.Ordering.Application.Checkout;

/// <summary>
/// How the customer wants the order delivered. Every part is optional: without a method, Shipping picks the first
/// configured method that can serve the order (same-day where possible, otherwise courier).
/// </summary>
public sealed record DeliveryChoice(string? Method, string? District, string? PickupPointId)
{
    public static readonly DeliveryChoice Default = new(null, null, null);
}
