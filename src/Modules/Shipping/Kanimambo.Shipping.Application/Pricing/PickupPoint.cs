namespace Kanimambo.Shipping.Application.Pricing;

public sealed class PickupPoint
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public decimal MaxKg { get; set; } = 30m;
}
