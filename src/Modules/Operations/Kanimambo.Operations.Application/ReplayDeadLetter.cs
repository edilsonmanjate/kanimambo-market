using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Operations.Application;

/// <summary>Deliver a dead letter again, after the cause was fixed; subscribers that already handled it are skipped.</summary>
public sealed record ReplayDeadLetter(string Module, Guid MessageId) : ICommand;
