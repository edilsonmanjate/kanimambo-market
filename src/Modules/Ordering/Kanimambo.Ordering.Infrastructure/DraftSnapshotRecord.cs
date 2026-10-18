namespace Kanimambo.Ordering.Infrastructure;

internal sealed class DraftSnapshotRecord
{
    public required Guid DraftId { get; init; }

    public required int Version { get; init; }

    public required string Payload { get; init; }
}
