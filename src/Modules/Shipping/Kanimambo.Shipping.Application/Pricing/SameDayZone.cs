namespace Kanimambo.Shipping.Application.Pricing;

public sealed class SameDayZone
{
    public string Name { get; set; } = string.Empty;

    public decimal FeeMzn { get; set; }

    public List<string> Districts { get; } = [];
}
