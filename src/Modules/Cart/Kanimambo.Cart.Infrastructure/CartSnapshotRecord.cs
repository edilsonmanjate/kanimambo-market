namespace Kanimambo.Cart.Infrastructure;

/// <summary>A stored snapshot: the serialized memento, opaque to the database.</summary>
internal sealed class CartSnapshotRecord
{
    public required Guid CartId { get; init; }

    public required int Version { get; init; }

    public required DateTimeOffset TakenAt { get; init; }

    public required string Payload { get; init; }
}
