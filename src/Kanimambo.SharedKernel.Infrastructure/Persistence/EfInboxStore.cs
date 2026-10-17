using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

/// <summary>A processed <see cref="IIdempotentCommand"/> key, stored in the owning module's schema.</summary>
public sealed class InboxRecord
{
    public required Guid IdempotencyKey { get; init; }

    public required string CommandName { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}

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

public static class InboxModelBuilderExtensions
{
    /// <summary>Maps the inbox table; the primary key resolves concurrent duplicate deliveries.</summary>
    public static ModelBuilder AddInbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<InboxRecord>(inbox =>
        {
            inbox.ToTable("InboxRecords");
            inbox.HasKey(r => r.IdempotencyKey);
            inbox.Property(r => r.CommandName).HasMaxLength(200);
        });

        return modelBuilder;
    }
}
