namespace Kanimambo.SharedKernel.Operations;

/// <summary>A module's outbox at a glance: messages still to deliver, messages given up on, and how old the oldest pending one is.</summary>
public sealed record OutboxSummary(string Module, int Pending, int DeadLettered, DateTimeOffset? OldestPendingAt);
