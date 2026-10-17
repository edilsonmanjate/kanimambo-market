using System.Text.Json;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kanimambo.SharedKernel.Infrastructure.Outbox;

/// <summary>
/// On SaveChanges, turns the domain events of tracked aggregates into outbox messages in the same
/// transaction. If the change commits, the events are guaranteed to be delivered; if it rolls back, they vanish.
/// </summary>
public sealed class OutboxInterceptor<TContext>(IIntegrationEventMapper mapper, TimeProvider timeProvider) : SaveChangesInterceptor
    where TContext : DbContext
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            WriteOutbox(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            WriteOutbox(context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void WriteOutbox(DbContext context)
    {
        var aggregates = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var integrationEvent in aggregate.DomainEvents.SelectMany(mapper.Map))
            {
                var type = integrationEvent.GetType();
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Id = integrationEvent.EventId,
                    Type = $"{type.FullName}, {type.Assembly.GetName().Name}",
                    Payload = JsonSerializer.Serialize(integrationEvent, type, Json),
                    OccurredAt = integrationEvent.OccurredAt,
                    NextAttemptAt = timeProvider.GetUtcNow(),
                });
            }

            aggregate.ClearDomainEvents();
        }
    }
}
