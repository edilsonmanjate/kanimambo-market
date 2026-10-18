using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.SharedKernel.Messaging;

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
