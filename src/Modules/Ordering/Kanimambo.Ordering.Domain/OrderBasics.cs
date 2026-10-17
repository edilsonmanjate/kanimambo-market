using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Domain;

/// <summary>Random (v4): guest customers follow their order by id, so it must not be guessable.</summary>
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

/// <summary>Prototype: an object that can produce an independent deep copy of itself.</summary>
public interface IPrototype<out T>
{
    T DeepClone();
}

/// <summary>Who placed the order. Guests have an empty <see cref="Id"/> but always a phone (SMS is the main channel).</summary>
public sealed record OrderCustomer(string Id, string Type, string Phone, string Nuit)
{
    public const string BusinessType = "b2b";

    public bool IsBusiness => Type == BusinessType;
}

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

public sealed record OrderStatusChange(OrderStatus From, OrderStatus To, DateTimeOffset At, string? Reason);
