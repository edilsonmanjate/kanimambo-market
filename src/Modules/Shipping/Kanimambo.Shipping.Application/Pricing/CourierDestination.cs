namespace Kanimambo.Shipping.Application.Pricing;

public sealed class CourierDestination
{
    public decimal BaseFeeMzn { get; set; }

    public decimal PerKgMzn { get; set; }

    public int TransitDays { get; set; }
}
