namespace Kanimambo.Ordering.Application;

/// <summary>Limits per phone number in a rolling 24 h window. Values come from configuration.</summary>
public sealed record AntiFraudSettings(int MaxOrdersPerPhonePerDay, decimal MaxValuePerPhonePerDayMzn);
