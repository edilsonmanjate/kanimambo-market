namespace Kanimambo.SharedKernel.Infrastructure.Features;

public sealed class FeatureFlagOptions
{
    public const string SectionName = "Features";

    public Dictionary<string, bool> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
}
