using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Domain;

/// <summary>One product in an order or draft, priced in MZN at checkout.</summary>
public sealed class OrderLine : IPrototype<OrderLine>
{
    private decimal _unitPriceMzn;
    private decimal _lineTotalMzn;

    private OrderLine()
    {
    }

    public OrderLine(Guid productId, Guid sellerId, string sku, string name, int quantity, Money unitPrice, Money lineTotal)
    {
        ArgumentNullException.ThrowIfNull(unitPrice);
        ArgumentNullException.ThrowIfNull(lineTotal);
        ProductId = productId;
        SellerId = sellerId;
        Sku = sku;
        Name = name;
        Quantity = quantity;
        _unitPriceMzn = unitPrice.Amount;
        _lineTotalMzn = lineTotal.Amount;
    }

    public Guid ProductId { get; private set; }

    public Guid SellerId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public Money UnitPrice => Money.Mzn(_unitPriceMzn);

    public Money LineTotal => Money.Mzn(_lineTotalMzn);

    /// <summary>A brand-new line with the same values: changing the copy never touches the original.</summary>
    public OrderLine DeepClone() => new(ProductId, SellerId, Sku, Name, Quantity, UnitPrice, LineTotal);

    /// <summary>Only drafts adjust quantities; placed orders are immutable.</summary>
    internal void ChangeQuantity(int quantity)
    {
        Quantity = quantity;
        _lineTotalMzn = Money.Mzn(_unitPriceMzn * quantity).Amount;
    }
}
