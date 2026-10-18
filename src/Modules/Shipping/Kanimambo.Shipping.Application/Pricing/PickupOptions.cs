namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>Free collection at marketplace pickup points.</summary>
public sealed class PickupOptions
{
    public List<PickupPoint> Points { get; } = [];
}
