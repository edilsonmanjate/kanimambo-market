using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// <see cref="IUnitOfWork"/> over one module's DbContext. Runs inside the provider's execution strategy
/// (Aspire enables retries on transient Npgsql failures), so the whole operation is retried as a unit.
/// </summary>
public sealed class EfUnitOfWork<TContext>(TContext db) : IUnitOfWork
    where TContext : DbContext
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        Func<TResult, bool> commitWhen,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(commitWhen);

        // A command dispatched from inside another command joins the outer transaction.
        if (db.Database.CurrentTransaction is not null)
        {
            return await operation();
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async ct =>
            {
                // On a retry, start from a clean slate rather than replaying stale tracked changes.
                db.ChangeTracker.Clear();

                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var result = await operation();

                if (!commitWhen(result))
                {
                    await transaction.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                    return result;
                }

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            },
            cancellationToken);
    }
}
