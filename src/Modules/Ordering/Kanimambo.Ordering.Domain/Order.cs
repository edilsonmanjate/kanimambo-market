using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

/// <summary>Domain events carry what their subscribers need, so they can be published without reloading the order.</summary>
public sealed record OrderPlaced(OrderId OrderId, long Number, string CheckoutSource, Guid CheckoutSourceId, string CustomerId, Money Total, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt);

public sealed record OrderPaid(OrderId OrderId, long Number, string PaymentReference, OrderCustomer Customer, PaymentMethod PaymentMethod, Money Total,
    IReadOnlyList<OrderLine> Lines, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

public sealed record OrderStatusChanged(OrderId OrderId, long Number, string CustomerPhone, OrderStatus From, OrderStatus To, string? Reason, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt);

/// <summary>The amounts of an order, all in MZN: subtotal − promotion + shipping = total.</summary>
public sealed record OrderTotals(Money Subtotal, string PromotionName, Money Discount, Money ShippingFee)
{
    public Money Total => Subtotal - Discount + ShippingFee;
}

/// <summary>
/// An accepted order and its lifecycle. Behaviour that depends on the status is delegated to the current
/// <see cref="OrderState"/>; the order itself only knows how to record a transition.
/// </summary>
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderStatusChange> _history = [];
    private decimal _subtotal;
    private decimal _discount;
    private decimal _shippingFee;

    private Order()
    {
    }

    private Order(OrderId id, long number, string checkoutSource, Guid checkoutSourceId, OrderCustomer customer, string deliveryCity,
        PaymentMethod paymentMethod, IEnumerable<OrderLine> lines, OrderTotals totals, DateTimeOffset now)
        : base(id)
    {
        Number = number;
        CheckoutSource = checkoutSource;
        CheckoutSourceId = checkoutSourceId;
        Customer = customer;
        DeliveryCity = deliveryCity;
        PaymentMethod = paymentMethod;
        _lines.AddRange(lines);
        _subtotal = totals.Subtotal.Amount;
        _discount = totals.Discount.Amount;
        _shippingFee = totals.ShippingFee.Amount;
        PromotionName = totals.PromotionName;
        Status = OrderStatus.Pending;
        PlacedAt = UpdatedAt = now;
        Raise(new OrderPlaced(id, number, checkoutSource, checkoutSourceId, customer.Id, Totals.Total, now));
    }

    /// <summary>Human-friendly sequential number (KM-1001…), from a database sequence.</summary>
    public long Number { get; private set; }

    /// <summary>The cart or draft this order came from; unique, which makes checkout idempotent.</summary>
    public string CheckoutSource { get; private set; } = string.Empty;

    public Guid CheckoutSourceId { get; private set; }

    public OrderCustomer Customer { get; private set; } = null!;

    public string DeliveryCity { get; private set; } = string.Empty;

    public PaymentMethod PaymentMethod { get; private set; }

    public string? PaymentReference { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string PromotionName { get; private set; } = string.Empty;

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public IReadOnlyList<OrderStatusChange> History => _history.AsReadOnly();

    public OrderTotals Totals => new(Money.Mzn(_subtotal), PromotionName, Money.Mzn(_discount), Money.Mzn(_shippingFee));

    public bool PaysOnDelivery => PaymentMethod == PaymentMethod.CashOnDelivery;

    public string DisplayNumber => $"KM-{Number}";

    public OrderState State => OrderState.Of(Status);

    public IReadOnlyList<OrderAction> AllowedActions => State.AllowedActions(this);

    public static Result<Order> Place(
        OrderId id,
        long number,
        string checkoutSource,
        Guid checkoutSourceId,
        OrderCustomer customer,
        string deliveryCity,
        PaymentMethod paymentMethod,
        IReadOnlyCollection<OrderLine> lines,
        OrderTotals totals,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(totals);

        if (lines.Count == 0)
        {
            return Error.BusinessRule("ordering.empty", "An order needs at least one product.");
        }

        if (paymentMethod == PaymentMethod.None)
        {
            return Error.Validation(new FieldError("paymentMethod", "Choose how to pay."));
        }

        // Placed orders own copies of the lines: later edits to the source draft never reach them.
        return new Order(id, number, checkoutSource, checkoutSourceId, customer, deliveryCity, paymentMethod,
            lines.Select(l => l.DeepClone()), totals, now);
    }

    public Result RequestPayment(DateTimeOffset now) => State.RequestPayment(this, now);

    public Result ConfirmPayment(string reference, DateTimeOffset now) => State.ConfirmPayment(this, reference, now);

    public Result FailPayment(string reason, DateTimeOffset now) => State.FailPayment(this, reason, now);

    public Result RetryPayment(DateTimeOffset now) => State.RetryPayment(this, now);

    public Result StartPreparing(DateTimeOffset now) => State.StartPreparing(this, now);

    public Result Dispatch(DateTimeOffset now) => State.Dispatch(this, now);

    public Result Deliver(DateTimeOffset now) => State.Deliver(this, now);

    public Result Cancel(string reason, DateTimeOffset now) => State.Cancel(this, reason, now);

    public Result Refund(string reason, DateTimeOffset now) => State.Refund(this, reason, now);

    /// <summary>Records a transition. Only states call this, after deciding the transition is allowed.</summary>
    internal Result MoveTo(OrderStatus next, DateTimeOffset now, string? reason = null)
    {
        var previous = Status;
        Status = next;
        UpdatedAt = now;
        _history.Add(new OrderStatusChange(previous, next, now, reason));
        Raise(new OrderStatusChanged(Id, Number, Customer.Phone, previous, next, reason, now));
        return Result.Success();
    }

    internal Result MarkPaid(string reference, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Error.Validation(new FieldError("reference", "A payment reference (gateway transaction id) is required."));
        }

        PaymentReference = reference.Trim();
        MoveTo(OrderStatus.Paid, now);
        RaisePaid(now);
        return Result.Success();
    }

    internal Result MarkDelivered(DateTimeOffset now)
    {
        MoveTo(OrderStatus.Delivered, now);
        if (PaysOnDelivery)
        {
            // Cash collected by the courier: this is when a cash-on-delivery order is paid.
            PaymentReference = $"COD-{DisplayNumber}";
            RaisePaid(now);
        }

        return Result.Success();
    }

    private void RaisePaid(DateTimeOffset now) =>
        Raise(new OrderPaid(Id, Number, PaymentReference!, Customer, PaymentMethod, Totals.Total, [.. _lines.Select(l => l.DeepClone())], now));
}
