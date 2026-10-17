using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>A message with exactly one handler that produces <typeparamref name="TResponse"/>.</summary>
public interface IRequest<out TResponse>;

/// <summary>Marker shared by every command, used to scope behaviors (unit of work) to writes only.</summary>
public interface ICommandBase;

/// <summary>A state-changing request that returns no payload.</summary>
public interface ICommand : IRequest<Result>, ICommandBase;

/// <summary>A state-changing request that returns a payload (e.g. the id of what was created).</summary>
public interface ICommand<TValue> : IRequest<Result<TValue>>, ICommandBase;

/// <summary>A read-only request. Never runs inside a unit of work.</summary>
public interface IQuery<TValue> : IRequest<Result<TValue>>;

/// <summary>
/// A command that may be delivered more than once (outbox relay, payment-gateway callbacks)
/// and must take effect only once. See <see cref="Behaviors.IdempotencyBehavior{TRequest, TResponse}"/>.
/// </summary>
public interface IIdempotentCommand : ICommand
{
    Guid IdempotencyKey { get; }
}

/// <summary>An in-process message with zero or more handlers.</summary>
public interface INotification;
