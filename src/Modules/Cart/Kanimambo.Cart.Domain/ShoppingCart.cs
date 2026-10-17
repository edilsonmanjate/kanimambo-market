using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Domain;

public readonly record struct CartId(Guid Value)
{
    /// <summary>Random (v4) on purpose: a guest cart id is what grants access to it, so it must not be guessable.</summary>
    public static CartId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

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

/// <summary>
/// A shopping cart (the Memento originator). Every change bumps <see cref="Version"/>; the application stores
/// a <see cref="CartMemento"/> after each change so a customer who lost connectivity, or changed device,
/// can go back to any recent state.
/// </summary>
public sealed class ShoppingCart : AggregateRoot<CartId>
{
    public const int MaxLines = 100;
    public const int MaxQuantityPerLine = 999;

    private readonly List<CartLine> _lines = [];
    private string? _customerId;
    private string? _customerType;

    private ShoppingCart()
    {
    }

    private ShoppingCart(CartId id, Customer customer, string deliveryCity, DateTimeOffset now)
        : base(id)
    {
        _customerId = string.IsNullOrEmpty(customer.Id) ? null : customer.Id;
        _customerType = _customerId is null ? null : customer.Type;
        DeliveryCity = deliveryCity;
        Version = 1;
        CreatedAt = UpdatedAt = now;
    }

    public Customer Customer => Customer.Restore(_customerId, _customerType);

    public string DeliveryCity { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<CartLine> Lines => _lines.AsReadOnly();

    public int ItemCount => _lines.Sum(l => l.Quantity);

    public static Result<ShoppingCart> Open(CartId id, Customer customer, string deliveryCity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return string.IsNullOrWhiteSpace(deliveryCity)
            ? Error.Validation(new FieldError("deliveryCity", "Tell us where to deliver (e.g. Maputo, Matola, Beira, Nampula)."))
            : new ShoppingCart(id, customer, deliveryCity.Trim(), now);
    }

    /// <summary>Sets the quantity of a product; 0 removes the line.</summary>
    public Result SetQuantity(Guid productId, string sku, string name, int quantity, DateTimeOffset now)
    {
        if (quantity is < 0 or > MaxQuantityPerLine)
        {
            return Error.Validation(new FieldError("quantity", $"Quantity must be between 0 and {MaxQuantityPerLine}."));
        }

        var line = _lines.Find(l => l.ProductId == productId);
        if (quantity == 0)
        {
            return line is null ? Result.Success() : Remove(productId, now);
        }

        if (line is null)
        {
            if (_lines.Count == MaxLines)
            {
                return Error.BusinessRule("cart.too_many_lines", $"A cart can hold at most {MaxLines} different products.");
            }

            _lines.Add(new CartLine(productId, sku, name, quantity));
        }
        else
        {
            line.ChangeQuantity(quantity);
        }

        Touch(now);
        return Result.Success();
    }

    public Result Remove(Guid productId, DateTimeOffset now)
    {
        var removed = _lines.RemoveAll(l => l.ProductId == productId);
        if (removed == 0)
        {
            return Error.NotFound("cart.line_not_found", $"Product {productId} is not in the cart.");
        }

        Touch(now);
        return Result.Success();
    }

    public Result ChangeDeliveryCity(string city, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return Error.Validation(new FieldError("deliveryCity", "Delivery city is required."));
        }

        DeliveryCity = city.Trim();
        Touch(now);
        return Result.Success();
    }

    public CartMemento CreateMemento() =>
        new(Version, UpdatedAt, DeliveryCity, [.. _lines.Select(l => new CartMemento.Line(l.ProductId, l.Sku, l.Name, l.Quantity))]);

    /// <summary>Returns the cart to the snapshotted state, including its version number.</summary>
    public Result Restore(CartMemento memento, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(memento);
        if (memento.Version > Version)
        {
            return Error.BusinessRule("cart.snapshot_from_future", $"Snapshot {memento.Version} is newer than the cart (version {Version}).");
        }

        _lines.Clear();
        _lines.AddRange(memento.Lines.Select(l => new CartLine(l.ProductId, l.Sku, l.Name, l.Quantity)));
        DeliveryCity = memento.DeliveryCity;
        Version = memento.Version;
        UpdatedAt = now;
        return Result.Success();
    }

    private void Touch(DateTimeOffset now)
    {
        Version++;
        UpdatedAt = now;
    }
}
