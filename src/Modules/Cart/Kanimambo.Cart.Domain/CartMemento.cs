using System.Collections.Immutable;

namespace Kanimambo.Cart.Domain;

/// <summary>
/// Memento: an immutable, serializable snapshot of a cart's editable state at one version. The cart creates
/// it and restores from it; the history (caretaker) only stores and hands it back. Ownership is deliberately
/// not part of the snapshot — restoring never changes who the cart belongs to.
/// </summary>
public sealed record CartMemento(int Version, DateTimeOffset TakenAt, string DeliveryCity, ImmutableArray<CartMemento.Line> Lines)
{
    public sealed record Line(Guid ProductId, string Sku, string Name, int Quantity);

    public int ItemCount => Lines.Sum(l => l.Quantity);

    public bool Equals(CartMemento? other) =>
        other is not null
        && Version == other.Version
        && TakenAt == other.TakenAt
        && DeliveryCity == other.DeliveryCity
        && Lines.SequenceEqual(other.Lines);

    public override int GetHashCode() => HashCode.Combine(Version, TakenAt, DeliveryCity, Lines.Length);
}
