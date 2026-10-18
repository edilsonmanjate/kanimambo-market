namespace Kanimambo.Shipping.Application.Pricing;

public sealed class SameDayVehicle
{
    public string Name { get; set; } = string.Empty;

    public decimal MaxKg { get; set; }

    public decimal SurchargeMzn { get; set; }
}
