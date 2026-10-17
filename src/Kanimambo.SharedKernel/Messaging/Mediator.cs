using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
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

internal abstract class RequestHandlerWrapper<TResponse>
{
    public abstract ValueTask<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken);
}

internal sealed class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override ValueTask<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
            ?? throw new InvalidOperationException($"No handler registered for '{typeof(TRequest).FullName}'.");

        RequestHandlerNext<TResponse> pipeline = () => handler.Handle(typedRequest, cancellationToken);

        // Registration order == execution order: the first registered behavior is the outermost,
        // so the chain is built from the last behavior inwards.
        var behaviors = services.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
        }

        return pipeline();
    }
}

internal abstract class NotificationHandlerWrapper
{
    public abstract IEnumerable<(string HandlerName, Func<CancellationToken, ValueTask> Invoke)> GetHandlers(
        INotification notification, IServiceProvider services);
}

internal sealed class NotificationHandlerWrapper<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    public override IEnumerable<(string HandlerName, Func<CancellationToken, ValueTask> Invoke)> GetHandlers(
        INotification notification, IServiceProvider services)
    {
        var typedNotification = (TNotification)notification;
        foreach (var handler in services.GetServices<INotificationHandler<TNotification>>())
        {
            yield return (handler.GetType().Name, ct => handler.Handle(typedNotification, ct));
        }
    }
}
