using Kanimambo.Cart.Domain;

namespace Kanimambo.Cart.Application;

public sealed record CartSnapshotDto(int Version, DateTimeOffset TakenAt, string DeliveryCity, int ItemCount, IReadOnlyList<CartLineDto> Lines)
{
    public static CartSnapshotDto From(CartMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        return new CartSnapshotDto(memento.Version, memento.TakenAt, memento.DeliveryCity, memento.ItemCount,
            [.. memento.Lines.Select(l => new CartLineDto(l.ProductId, l.Sku, l.Name, l.Quantity))]);
    }
}
