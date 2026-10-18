namespace Kanimambo.Simulators.ExchangeRates;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public int LatencyMilliseconds { get; set; } = 800;

    public int CallsPerMinute { get; set; } = 30;

    /// <summary>Reference rates to MZN; each day varies deterministically by up to ±0.5 %.</summary>
    public Dictionary<string, decimal> BaseRatesToMzn { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 63.90m,
        ["ZAR"] = 3.55m,
    };
}
