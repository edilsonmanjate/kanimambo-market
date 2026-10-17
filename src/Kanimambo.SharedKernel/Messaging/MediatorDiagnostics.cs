using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Kanimambo.SharedKernel.Messaging;

public static class MediatorDiagnostics
{
    public const string ActivitySourceName = "Kanimambo.Mediator";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}

internal static partial class MediatorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs:0.0} ms")]
    public static partial void Handled(ILogger logger, string requestName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} failed with {ErrorCode} ({ErrorType}) in {ElapsedMs:0.0} ms")]
    public static partial void HandledWithFailure(ILogger logger, string requestName, string errorCode, string errorType, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "{RequestName} threw after {ElapsedMs:0.0} ms")]
    public static partial void Threw(ILogger logger, Exception exception, string requestName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification handler {HandlerName} failed for {NotificationName}")]
    public static partial void NotificationHandlerFailed(ILogger logger, Exception exception, string handlerName, string notificationName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped {RequestName}: idempotency key {IdempotencyKey} was already processed")]
    public static partial void DuplicateSkipped(ILogger logger, string requestName, Guid idempotencyKey);
}
