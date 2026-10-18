using System.Collections.Immutable;

namespace Kanimambo.Ordering.Domain;

/// <summary>Memento of a draft: version and quantities, immutable and serializable.</summary>
public sealed record DraftOrderMemento(int Version, DateTimeOffset TakenAt, ImmutableArray<DraftOrderMemento.Line> Lines)
{
    public sealed record Line(Guid ProductId, int Quantity);

    public bool Equals(DraftOrderMemento? other) =>
        other is not null && Version == other.Version && TakenAt == other.TakenAt && Lines.SequenceEqual(other.Lines);

    public override int GetHashCode() => HashCode.Combine(Version, TakenAt, Lines.Length);
}
