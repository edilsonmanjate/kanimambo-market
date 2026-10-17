using System.Diagnostics;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.SharedKernel.Messaging.Behaviors;

/// <summary>Outermost behavior: structured log + OpenTelemetry span per request, with duration and outcome.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    TimeProvider timeProvider) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerNext<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var requestName = typeof(TRequest).Name;
        using var activity = MediatorDiagnostics.ActivitySource.StartActivity($"mediator {requestName}");
        activity?.SetTag("kanimambo.request.name", requestName);

        var started = timeProvider.GetTimestamp();
        try
        {
            var response = await next();
            var elapsedMs = timeProvider.GetElapsedTime(started).TotalMilliseconds;

            if (response is Result { IsFailure: true } failed)
            {
                activity?.SetStatus(ActivityStatusCode.Error, failed.Error.Code);
                activity?.SetTag("kanimambo.error.code", failed.Error.Code);
                MediatorLog.HandledWithFailure(logger, requestName, failed.Error.Code, failed.Error.Type.ToString(), elapsedMs);
            }
            else
            {
                MediatorLog.Handled(logger, requestName, elapsedMs);
            }

            return response;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            MediatorLog.Threw(logger, exception, requestName, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
