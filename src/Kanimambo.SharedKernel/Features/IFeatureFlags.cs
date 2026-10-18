namespace Kanimambo.SharedKernel.Features;

/// <summary>
/// Feature flags for gradual rollouts (e.g. a new export format). One shared, thread-safe instance for the whole
/// application, provided as a DI singleton — never a static <c>Instance</c>, so tests can swap it.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>False for flags that are not configured: features are off unless switched on.</summary>
    bool IsEnabled(string flag);
}
