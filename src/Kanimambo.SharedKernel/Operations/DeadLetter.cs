namespace Kanimambo.SharedKernel.Operations;

/// <summary>A message the outbox stopped retrying: which subscribers got it, and the last error of the ones that did not.</summary>
public sealed record DeadLetter(
    Guid Id,
    string Module,
    string EventType,
    DateTimeOffset OccurredAt,
    int Attempts,
    string? LastError,
    IReadOnlyList<string> DeliveredTo,
    string Payload);
