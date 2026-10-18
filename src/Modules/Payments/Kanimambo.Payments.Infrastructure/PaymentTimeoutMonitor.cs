using Kanimambo.Payments.Application;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Payments.Infrastructure;

/// <summary>Resolves expired PIN prompts every few seconds (asks the operator, then times the payment out).</summary>
internal sealed partial class PaymentTimeoutMonitor(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    PaymentsOptions options,
    ILogger<PaymentTimeoutMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.TimeoutCheckInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpirePendingPayments(), stoppingToken);
                if (result.IsSuccess && result.Value > 0)
                {
                    LogResolved(logger, result.Value);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Count} expired mobile-money payment(s)")]
    private static partial void LogResolved(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment timeout check failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
