namespace Kanimambo.SharedKernel.Messaging;

public interface ISender
{
    ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

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

/// <summary>The only dependency controllers need: dispatches commands, queries and notifications in-process.</summary>
public interface IMediator : ISender, IPublisher;
