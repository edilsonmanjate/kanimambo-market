using Kanimambo.SharedKernel.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.SharedKernel.Infrastructure.Outbox;

/// <summary>The operations view of one module's outbox table; each call uses its own scope and saves its own change.</summary>
public sealed class OutboxAdministration<TContext>(IServiceScopeFactory scopeFactory, string module, TimeProvider timeProvider) : IOutboxAdministration
    where TContext : DbContext
{
    public string Module => module;

    public async Task<OutboxSummary> SummaryAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<TContext>().Set<OutboxMessage>().AsNoTracking();
        var pending = outbox.Where(m => m.ProcessedAt == null && !m.DeadLettered);
        return new OutboxSummary(
            module,
            await pending.CountAsync(cancellationToken),
            await outbox.CountAsync(m => m.DeadLettered, cancellationToken),
            await pending.OrderBy(m => m.OccurredAt).Select(m => (DateTimeOffset?)m.OccurredAt).FirstOrDefaultAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<DeadLetter>> ListDeadLettersAsync(int max, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var messages = await scope.ServiceProvider.GetRequiredService<TContext>().Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.DeadLettered)
            .OrderByDescending(m => m.OccurredAt)
            .Take(max)
            .ToListAsync(cancellationToken);
        return [.. messages.Select(m => new DeadLetter(m.Id, module, EventName(m.Type), m.OccurredAt, m.Attempts, m.LastError, m.DeliveredTo, m.Payload))];
    }

    public async Task<bool> ReplayAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await db.Set<OutboxMessage>().SingleOrDefaultAsync(m => m.Id == messageId && m.DeadLettered, cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.DeadLettered = false;
        message.Attempts = 0;
        message.LockedUntil = null;
        message.NextAttemptAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary><c>Namespace.OrderPaidIntegrationEvent, Assembly</c> → <c>OrderPaidIntegrationEvent</c>.</summary>
    private static string EventName(string type)
    {
        var name = type.Split(',')[0];
        return name[(name.LastIndexOf('.') + 1)..];
    }
}
