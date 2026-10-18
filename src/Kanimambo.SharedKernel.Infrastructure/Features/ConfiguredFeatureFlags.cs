using System.Collections.Frozen;
using Kanimambo.SharedKernel.Features;
using Microsoft.Extensions.Options;

namespace Kanimambo.SharedKernel.Infrastructure.Features;

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
