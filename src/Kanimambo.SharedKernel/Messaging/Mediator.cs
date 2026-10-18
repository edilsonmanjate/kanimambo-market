using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>
/// In-process mediator. Reflection is used once per message type to build a strongly typed
/// wrapper, which is cached; every later dispatch goes through generic code only.
/// </summary>
internal sealed class Mediator(IServiceProvider services, ILogger<Mediator> logger) : IMediator
{
    private static readonly ConcurrentDictionary<Type, object> RequestWrappers = new();
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> NotificationWrappers = new();

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)RequestWrappers.GetOrAdd(
            request.GetType(),
            static requestType => Activator.CreateInstance(
                typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse)))!);

        return wrapper.Handle(request, services, cancellationToken);
    }

    public async ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var wrapper = NotificationWrappers.GetOrAdd(
            notification.GetType(),
            static notificationType => (NotificationHandlerWrapper)Activator.CreateInstance(
                typeof(NotificationHandlerWrapper<>).MakeGenericType(notificationType))!);

        List<Exception>? failures = null;
        foreach (var (handlerName, invoke) in wrapper.GetHandlers(notification, services))
        {
            try
            {
                await invoke(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                MediatorLog.NotificationHandlerFailed(logger, exception, handlerName, notification.GetType().Name);
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(
                $"{failures.Count} handler(s) failed for notification '{notification.GetType().Name}'.", failures);
        }
    }
}
