using System.Collections.Frozen;
using Kanimambo.SharedKernel.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kanimambo.SharedKernel.Infrastructure.Features;

public sealed class FeatureFlagOptions
{
    public const string SectionName = "Features";

    public Dictionary<string, bool> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Singleton, thread-safe: readers see an immutable snapshot; a configuration reload builds a new snapshot and
/// swaps the reference atomically, so a request never sees half of an update and nobody takes a lock to read.
/// </summary>
public sealed class ConfiguredFeatureFlags : IFeatureFlags, IDisposable
{
    private readonly IDisposable? _subscription;
    private FrozenDictionary<string, bool> _flags;

    public ConfiguredFeatureFlags(IOptionsMonitor<FeatureFlagOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _flags = Snapshot(options.CurrentValue);
        _subscription = options.OnChange(o => Volatile.Write(ref _flags, Snapshot(o)));
    }

    public bool IsEnabled(string flag) => Volatile.Read(ref _flags).TryGetValue(flag, out var enabled) && enabled;

    public void Dispose() => _subscription?.Dispose();

    private static FrozenDictionary<string, bool> Snapshot(FeatureFlagOptions options) =>
        options.Flags.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
}

public static class FeatureFlagExtensions
{
    /// <summary>One instance for the application lifetime: the DI container, not a static field, owns it.</summary>
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FeatureFlagOptions>().Bind(configuration.GetSection(FeatureFlagOptions.SectionName));
        services.AddSingleton<IFeatureFlags, ConfiguredFeatureFlags>();
        return services;
    }
}
