namespace Kanimambo.SharedKernel.Messaging;

public interface IPublisher
{
    /// <summary>
    /// Invokes every handler registered for the runtime type of <paramref name="notification"/>.
    /// A failing handler does not prevent the others from running; failures are reported
    /// together in an <see cref="AggregateException"/> after all handlers ran.
    /// </summary>
    ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
