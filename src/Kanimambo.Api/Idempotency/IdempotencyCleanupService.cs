using Microsoft.Extensions.Options;

namespace Kanimambo.Api.Idempotency;

/// <summary>Periodically deletes expired idempotency records.</summary>
internal sealed partial class IdempotencyCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<IdempotencyStore>().DeleteExpiredAsync(stoppingToken);
                LogCleanup(logger, deleted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Cleanup is best effort; the next tick tries again.
                LogCleanupFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted {Count} expired idempotency records")]
    private static partial void LogCleanup(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idempotency cleanup failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
