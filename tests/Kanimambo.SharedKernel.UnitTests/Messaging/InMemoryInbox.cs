using System.Collections.Concurrent;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class InMemoryInbox(CallLog log) : IInboxStore
{
    private readonly ConcurrentDictionary<Guid, string> _processed = new();

    public Task<bool> HasBeenProcessedAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(_processed.ContainsKey(idempotencyKey));

    public void MarkAsProcessed(Guid idempotencyKey, string commandName)
    {
        log.Add("inbox:mark");
        _processed[idempotencyKey] = commandName;
    }
}
