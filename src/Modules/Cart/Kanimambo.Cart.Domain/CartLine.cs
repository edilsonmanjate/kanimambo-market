namespace Kanimambo.Cart.Domain;

public sealed class CartLine
{
    private CartLine()
    {
    }

    internal CartLine(Guid productId, string sku, string name, int quantity)
    {
        ProductId = productId;
        Sku = sku;
        Name = name;
        Quantity = quantity;
    }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    internal void ChangeQuantity(int quantity) => Quantity = quantity;
}
