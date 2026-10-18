namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>
/// Every price, zone, vehicle limit and pickup point comes from configuration (<c>Shipping</c> section): delivery
/// prices change with fuel prices and new zones, and none of it should need a deploy.
/// </summary>
public sealed class ShippingOptions
{
    public const string SectionName = "Shipping";

    /// <summary>Order of preference when checkout does not name a method; the first method that can serve wins.</summary>
    public List<string> DefaultMethods { get; } = [];

    public SameDayOptions SameDay { get; set; } = new();

    public CourierOptions Courier { get; set; } = new();

    public PickupOptions Pickup { get; set; } = new();
}
