using System.Collections.Concurrent;

namespace Kanimambo.Ordering.IntegrationTests;

public sealed class FailurePlan
{
    public ConcurrentDictionary<Guid, int> FailTimes { get; } = new();

    public ConcurrentDictionary<Guid, bool> FailAlways { get; } = new();

    public ConcurrentDictionary<Guid, int> Calls { get; } = new();
}
