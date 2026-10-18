namespace Kanimambo.SharedKernel.Infrastructure.Outbox;

/// <summary>
/// An integration event waiting to be delivered. Written in the same transaction as the change that raised
/// it; delivery is tracked per subscriber, so a failing subscriber is retried alone.
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>CLR type of the event, <c>Namespace.Type, Assembly</c>.</summary>
    public required string Type { get; init; }

    public required string Payload { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>Subscribers (handler type names) that already handled this message successfully.</summary>
    public List<string> DeliveredTo { get; set; } = [];

    public bool DeadLettered { get; set; }
}
