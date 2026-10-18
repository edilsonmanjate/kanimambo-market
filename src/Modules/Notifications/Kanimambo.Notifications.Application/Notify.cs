using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Notifications.Application;

/// <summary>Send one message to the customer behind <paramref name="Phone"/>, on the first preferred channel that works.</summary>
public sealed record Notify(Guid IdempotencyKey, string Phone, MessageRequest Message) : IIdempotentCommand;
