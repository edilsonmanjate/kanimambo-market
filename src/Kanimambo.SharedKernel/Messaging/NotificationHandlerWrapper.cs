namespace Kanimambo.SharedKernel.Messaging;

internal abstract class NotificationHandlerWrapper
{
    public abstract IEnumerable<(string HandlerName, Func<CancellationToken, ValueTask> Invoke)> GetHandlers(
        INotification notification, IServiceProvider services);
}
