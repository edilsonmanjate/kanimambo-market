using System.Collections.Concurrent;

namespace Kanimambo.Api.IntegrationTests.Probes;

public sealed class ProbeState
{
    private readonly ConcurrentDictionary<string, int> _counts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new();

    public int Increment(string bucket) => _counts.AddOrUpdate(bucket, 1, (_, count) => count + 1);

    public int Count(string bucket) => _counts.GetValueOrDefault(bucket);

    public TaskCompletionSource Gate(string bucket) =>
        _gates.GetOrAdd(bucket, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
