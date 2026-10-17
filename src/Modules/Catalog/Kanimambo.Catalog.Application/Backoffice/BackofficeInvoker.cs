using System.Text.Json;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

/// <summary>
/// Command-pattern invoker: executes operations, records them in the audit log (who, what, when, and
/// the state needed to undo), and undoes them later from that record.
/// </summary>
public sealed class BackofficeInvoker(
    BackofficeContext context,
    IBackofficeOperationLog log,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<BackofficeOperationRecord>> ExecuteAsync(IBackofficeOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var executed = await operation.ExecuteAsync(context, cancellationToken);
        if (executed.IsFailure)
        {
            return executed.Error;
        }

        var record = BackofficeOperationRecord.Executed(
            operation.Kind,
            operation.Describe(),
            Serialize(operation),
            actor.Name,
            timeProvider.GetUtcNow());

        log.Add(record);
        return record;
    }

    public async Task<Result<BackofficeOperationRecord>> UndoAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var record = await log.GetAsync(operationId, cancellationToken);
        if (record is null)
        {
            return Error.NotFound("catalog.operation_not_found", $"Back-office operation {operationId} does not exist.");
        }

        var allowed = record.EnsureCanUndo();
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        var undone = await Deserialize(record.Payload).UndoAsync(context, cancellationToken);
        if (undone.IsFailure)
        {
            return undone.Error;
        }

        record.MarkUndone(actor.Name, timeProvider.GetUtcNow());
        return record;
    }

    internal static string Serialize(IBackofficeOperation operation) => JsonSerializer.Serialize(operation, Json);

    internal static IBackofficeOperation Deserialize(string payload) =>
        JsonSerializer.Deserialize<IBackofficeOperation>(payload, Json)
        ?? throw new InvalidOperationException("Stored back-office operation is empty.");
}
