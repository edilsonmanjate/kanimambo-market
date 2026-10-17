using System.Reflection;
using System.Text.Json;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kanimambo.SharedKernel.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 10;

    /// <summary>Retry delay after the first failure; doubles with each attempt (capped at five minutes).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Observer delivery: polls a module's outbox and notifies every subscriber of each event, each in its own
/// DI scope (own DbContext, own transaction). A subscriber that throws is recorded and retried with
/// exponential backoff; the subscribers that succeeded are never called again for that message.
/// Messages are claimed with <c>FOR UPDATE SKIP LOCKED</c> and a lease, so several API instances can run it.
/// </summary>
public sealed partial class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private static readonly MethodInfo DispatchMethod =
        typeof(OutboxProcessor<TContext>).GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollingInterval, timeProvider);
        do
        {
            try
            {
                while (await ProcessBatchAsync(stoppingToken) > 0 && !stoppingToken.IsCancellationRequested)
                {
                    // Keep draining while there is work.
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogBatchFailed(logger, exception, typeof(TContext).Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Claims and processes one batch; returns how many messages were claimed.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();

        var messages = await ClaimAsync(db, now, settings, cancellationToken);
        foreach (var message in messages)
        {
            await DeliverAsync(message, settings, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }

    private static Task<List<OutboxMessage>> ClaimAsync(TContext db, DateTimeOffset now, OutboxOptions settings, CancellationToken cancellationToken)
    {
        var table = $"\"{db.Model.GetDefaultSchema()}\".\"{OutboxModelBuilderExtensions.TableName}\"";
#pragma warning disable EF1002 // The table name comes from the model, never from input; all values are parameters.
        return db.Set<OutboxMessage>().FromSqlRaw(
            $$"""
            UPDATE {{table}} SET "LockedUntil" = {0}
            WHERE "Id" IN (
                SELECT "Id" FROM {{table}}
                WHERE "ProcessedAt" IS NULL AND NOT "DeadLettered" AND "NextAttemptAt" <= {1}
                  AND ("LockedUntil" IS NULL OR "LockedUntil" < {1})
                ORDER BY "OccurredAt"
                LIMIT {2}
                FOR UPDATE SKIP LOCKED)
            RETURNING *
            """,
            now + settings.LeaseDuration, now, settings.BatchSize).ToListAsync(cancellationToken);
#pragma warning restore EF1002
    }

    private async Task DeliverAsync(OutboxMessage message, OutboxOptions settings, CancellationToken cancellationToken)
    {
        var eventType = Type.GetType(message.Type, throwOnError: false);
        if (eventType is null)
        {
            Fail(message, $"Unknown event type '{message.Type}'.", settings, deadLetter: true);
            return;
        }

        var integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType, OutboxInterceptor<TContext>.Json)!;
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(eventType);

        IReadOnlyList<string> subscribers;
        await using (var discovery = scopeFactory.CreateAsyncScope())
        {
            subscribers = discovery.ServiceProvider.GetServices(handlerType).Select(h => h!.GetType().FullName!).ToList();
        }

        var errors = new List<string>();
        foreach (var subscriber in subscribers.Where(s => !message.DeliveredTo.Contains(s)))
        {
            try
            {
                // A fresh scope per subscriber: its own DbContext and unit of work, isolated from the others.
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetServices(handlerType).Single(h => h!.GetType().FullName == subscriber)!;
                await (Task)DispatchMethod.MakeGenericMethod(eventType).Invoke(null, [handler, integrationEvent, cancellationToken])!;
                message.DeliveredTo = [.. message.DeliveredTo, subscriber];
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogSubscriberFailed(logger, exception, subscriber, eventType.Name, message.Id);
                errors.Add($"{subscriber}: {exception.GetBaseException().Message}");
            }
        }

        if (errors.Count == 0)
        {
            message.ProcessedAt = timeProvider.GetUtcNow();
            message.LockedUntil = null;
            message.LastError = null;
            return;
        }

        Fail(message, string.Join(" | ", errors), settings, deadLetter: false);
    }

    private void Fail(OutboxMessage message, string error, OutboxOptions settings, bool deadLetter)
    {
        message.Attempts++;
        message.LastError = error.Length > 4000 ? error[..4000] : error;
        message.LockedUntil = null;
        message.DeadLettered = deadLetter || message.Attempts >= settings.MaxAttempts;

        var delay = TimeSpan.FromTicks(Math.Min(
            settings.RetryBaseDelay.Ticks * (1L << Math.Min(message.Attempts - 1, 20)),
            TimeSpan.FromMinutes(5).Ticks));
        message.NextAttemptAt = timeProvider.GetUtcNow() + delay;

        if (message.DeadLettered)
        {
            LogDeadLettered(logger, message.Id, message.Attempts, message.LastError);
        }
    }

    private static async Task DispatchAsync<TEvent>(object handler, object integrationEvent, CancellationToken cancellationToken)
        where TEvent : INotification =>
        await ((INotificationHandler<TEvent>)handler).Handle((TEvent)integrationEvent, cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed for {Context}")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception, string context);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Subscriber {Subscriber} failed for {EventType} {MessageId}; it will be retried")]
    private static partial void LogSubscriberFailed(ILogger logger, Exception exception, string subscriber, string eventType, Guid messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} dead-lettered after {Attempts} attempts: {Error}")]
    private static partial void LogDeadLettered(ILogger logger, Guid messageId, int attempts, string? error);
}
