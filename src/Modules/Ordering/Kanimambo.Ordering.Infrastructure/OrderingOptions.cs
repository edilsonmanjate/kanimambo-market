namespace Kanimambo.Ordering.Infrastructure;

public sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

    public int MaxOrdersPerPhonePerDay { get; set; } = 5;

    public decimal MaxValuePerPhonePerDayMzn { get; set; } = 100_000m;
}
