namespace Kanimambo.SharedKernel.Messaging;

/// <summary>
/// A command that may be delivered more than once (outbox relay, payment-gateway callbacks)
/// and must take effect only once. See <see cref="Behaviors.IdempotencyBehavior{TRequest, TResponse}"/>.
/// </summary>
public interface IIdempotentCommand : ICommand
{
    Guid IdempotencyKey { get; }
}
