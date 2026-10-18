using System.Collections.Concurrent;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

/// <summary>Records what ran, in order, so tests can assert on pipeline composition.</summary>
public sealed class CallLog
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => [.. _entries];

    public void Add(string entry) => _entries.Enqueue(entry);

    public int Count(string entry) => _entries.Count(e => e == entry);
}
