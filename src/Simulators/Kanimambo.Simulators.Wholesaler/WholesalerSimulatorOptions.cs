namespace Kanimambo.Simulators.Wholesaler;

public sealed class WholesalerSimulatorOptions
{
    public const string SectionName = "Simulator";

    public int LatencyMillisecondsPerPage { get; set; } = 300;

    public int ProductCount { get; set; } = 2_400;

    public int MaxPageSize { get; set; } = 200;
}
