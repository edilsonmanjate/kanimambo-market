using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Domain.Backoffice;

public enum BackofficeOperationStatus
{
    Executed,
    Undone,
}

/// <summary>
/// Audit-log entry for a back-office operation: who did what and when, plus the serialized operation
/// (including the state it needs to be undone).
/// </summary>
public sealed class BackofficeOperationRecord : Entity<Guid>
{
    private BackofficeOperationRecord()
    {
    }

    private BackofficeOperationRecord(Guid id, string kind, string description, string payload, string performedBy, DateTimeOffset performedAt)
        : base(id)
    {
        Kind = kind;
        Description = description;
        Payload = payload;
        PerformedBy = performedBy;
        PerformedAt = performedAt;
        Status = BackofficeOperationStatus.Executed;
    }

    public string Kind { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>The operation serialized as JSON, so it can be undone later.</summary>
    public string Payload { get; private set; } = string.Empty;

    public string PerformedBy { get; private set; } = string.Empty;

    public DateTimeOffset PerformedAt { get; private set; }

    public BackofficeOperationStatus Status { get; private set; }

    public string? UndoneBy { get; private set; }

    public DateTimeOffset? UndoneAt { get; private set; }

    public static BackofficeOperationRecord Executed(string kind, string description, string payload, string performedBy, DateTimeOffset performedAt) =>
        new(Guid.CreateVersion7(performedAt), kind, description, payload, performedBy, performedAt);

    public Result EnsureCanUndo() =>
        Status == BackofficeOperationStatus.Undone
            ? Error.Conflict("catalog.operation_already_undone", $"Operation was already undone by {UndoneBy} at {UndoneAt:u}.")
            : Result.Success();

    public Result MarkUndone(string undoneBy, DateTimeOffset undoneAt)
    {
        var allowed = EnsureCanUndo();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Status = BackofficeOperationStatus.Undone;
        UndoneBy = undoneBy;
        UndoneAt = undoneAt;
        return Result.Success();
    }
}
