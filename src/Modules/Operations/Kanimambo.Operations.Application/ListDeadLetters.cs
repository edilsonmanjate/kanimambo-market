using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Operations;

namespace Kanimambo.Operations.Application;

/// <summary>Messages the outbox gave up on, newest first; optionally for one module.</summary>
public sealed record ListDeadLetters(string? Module, int Max = 100) : IQuery<IReadOnlyList<DeadLetter>>;
