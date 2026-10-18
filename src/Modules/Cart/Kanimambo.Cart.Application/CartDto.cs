using Kanimambo.Cart.Domain;

namespace Kanimambo.Cart.Application;

public sealed record CartDto(Guid Id, int Version, string DeliveryCity, string CustomerType, IReadOnlyList<CartLineDto> Lines, int ItemCount, DateTimeOffset UpdatedAt, Guid? CheckedOutOrderId)
{
    public static CartDto From(ShoppingCart cart)
    {
        ArgumentNullException.ThrowIfNull(cart);
        return new CartDto(cart.Id.Value, cart.Version, cart.DeliveryCity, cart.Customer.Type,
            [.. cart.Lines.Select(l => new CartLineDto(l.ProductId, l.Sku, l.Name, l.Quantity))], cart.ItemCount, cart.UpdatedAt, cart.CheckedOutOrderId);
    }
}
