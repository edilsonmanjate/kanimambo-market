namespace Kanimambo.SharedKernel.Messaging;

/// <summary>A message with exactly one handler that produces <typeparamref name="TResponse"/>.</summary>
public interface IRequest<out TResponse>;
