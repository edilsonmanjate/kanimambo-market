using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

public sealed class EfInboxStore<TContext>(TContext db, TimeProvider timeProvider) : IInboxStore
    where TContext : DbContext
{
    public Task<bool> HasBeenProcessedAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
        db.Set<InboxRecord>().AnyAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);

    public void MarkAsProcessed(Guid idempotencyKey, string commandName) =>
        db.Set<InboxRecord>().Add(new InboxRecord
        {
            IdempotencyKey = idempotencyKey,
            CommandName = commandName,
            ProcessedAt = timeProvider.GetUtcNow(),
        });
}
