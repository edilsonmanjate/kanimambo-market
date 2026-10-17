using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

public enum OrderStatus
{
    Pending,
    AwaitingPayment,
    Paid,
    Preparing,
    OutForDelivery,
    Delivered,
    Cancelled,
    PaymentFailed,
    Refunded,
}

/// <summary>Everything that can be asked of an order.</summary>
public enum OrderAction
{
    RequestPayment,
    ConfirmPayment,
    FailPayment,
    RetryPayment,
    StartPreparing,
    Dispatch,
    Deliver,
    Cancel,
    Refund,
}

/// <summary>
/// State pattern: each status is an object that knows which actions it accepts and where they lead.
/// Every action is refused by default; a state only opens the transitions it overrides, so an invalid
/// transition (e.g. delivering an unpaid M-Pesa order) has no code path to happen.
/// </summary>
public abstract class OrderState
{
    private static readonly Dictionary<OrderStatus, OrderState> States = new OrderState[]
    {
        new PendingState(), new AwaitingPaymentState(), new PaidState(), new PreparingState(), new OutForDeliveryState(),
        new DeliveredState(), new CancelledState(), new PaymentFailedState(), new RefundedState(),
    }.ToDictionary(s => s.Status);

    public abstract OrderStatus Status { get; }

    public static OrderState Of(OrderStatus status) => States[status];

    /// <summary>Actions this state accepts for the given order (payment method can matter).</summary>
    public IReadOnlyList<OrderAction> AllowedActions(Order order) =>
        Enum.GetValues<OrderAction>().Where(action => Accepts(action, order)).ToList();

    protected virtual bool Accepts(OrderAction action, Order order) => false;

    public virtual Result RequestPayment(Order order, DateTimeOffset now) => Refuse(OrderAction.RequestPayment);

    public virtual Result ConfirmPayment(Order order, string reference, DateTimeOffset now) => Refuse(OrderAction.ConfirmPayment);

    public virtual Result FailPayment(Order order, string reason, DateTimeOffset now) => Refuse(OrderAction.FailPayment);

    public virtual Result RetryPayment(Order order, DateTimeOffset now) => Refuse(OrderAction.RetryPayment);

    public virtual Result StartPreparing(Order order, DateTimeOffset now) => Refuse(OrderAction.StartPreparing);

    public virtual Result Dispatch(Order order, DateTimeOffset now) => Refuse(OrderAction.Dispatch);

    public virtual Result Deliver(Order order, DateTimeOffset now) => Refuse(OrderAction.Deliver);

    public virtual Result Cancel(Order order, string reason, DateTimeOffset now) => Refuse(OrderAction.Cancel);

    public virtual Result Refund(Order order, string reason, DateTimeOffset now) => Refuse(OrderAction.Refund);

    protected Result Refuse(OrderAction action) =>
        Error.Conflict("ordering.invalid_transition", $"An order that is {Status} cannot {Describe(action)}.");

    private static string Describe(OrderAction action) => action switch
    {
        OrderAction.RequestPayment => "request payment",
        OrderAction.ConfirmPayment => "confirm payment",
        OrderAction.FailPayment => "fail payment",
        OrderAction.RetryPayment => "retry payment",
        OrderAction.StartPreparing => "start preparing",
        OrderAction.Dispatch => "be dispatched",
        OrderAction.Deliver => "be delivered",
        OrderAction.Cancel => "be cancelled",
        _ => "be refunded",
    };
}

/// <summary>Accepted by the acceptance chain; waiting for payment to be requested (or, cash on delivery, for preparation).</summary>
internal sealed class PendingState : OrderState
{
    public override OrderStatus Status => OrderStatus.Pending;

    protected override bool Accepts(OrderAction action, Order order) => action switch
    {
        OrderAction.RequestPayment => !order.PaysOnDelivery,
        OrderAction.StartPreparing => order.PaysOnDelivery,
        OrderAction.Cancel => true,
        _ => false,
    };

    public override Result RequestPayment(Order order, DateTimeOffset now) =>
        order.PaysOnDelivery
            ? Error.BusinessRule("ordering.cash_on_delivery", "Cash-on-delivery orders are paid to the courier, not requested.")
            : order.MoveTo(OrderStatus.AwaitingPayment, now);

    public override Result StartPreparing(Order order, DateTimeOffset now) =>
        order.PaysOnDelivery
            ? order.MoveTo(OrderStatus.Preparing, now)
            : Error.BusinessRule("ordering.not_paid", "The order must be paid before it is prepared.");

    public override Result Cancel(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.Cancelled, now, reason);
}

/// <summary>The customer was asked to confirm on the phone (M-Pesa / e-Mola / mKesh) or by card.</summary>
internal sealed class AwaitingPaymentState : OrderState
{
    public override OrderStatus Status => OrderStatus.AwaitingPayment;

    protected override bool Accepts(OrderAction action, Order order) =>
        action is OrderAction.ConfirmPayment or OrderAction.FailPayment or OrderAction.Cancel;

    public override Result ConfirmPayment(Order order, string reference, DateTimeOffset now) => order.MarkPaid(reference, now);

    public override Result FailPayment(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.PaymentFailed, now, reason);

    public override Result Cancel(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.Cancelled, now, reason);
}

/// <summary>Insufficient balance, wrong PIN, timeout... the customer may try again.</summary>
internal sealed class PaymentFailedState : OrderState
{
    public override OrderStatus Status => OrderStatus.PaymentFailed;

    protected override bool Accepts(OrderAction action, Order order) => action is OrderAction.RetryPayment or OrderAction.Cancel;

    public override Result RetryPayment(Order order, DateTimeOffset now) => order.MoveTo(OrderStatus.AwaitingPayment, now);

    public override Result Cancel(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.Cancelled, now, reason);
}

internal sealed class PaidState : OrderState
{
    public override OrderStatus Status => OrderStatus.Paid;

    protected override bool Accepts(OrderAction action, Order order) => action is OrderAction.StartPreparing or OrderAction.Refund;

    public override Result StartPreparing(Order order, DateTimeOffset now) => order.MoveTo(OrderStatus.Preparing, now);

    public override Result Refund(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.Refunded, now, reason);
}

/// <summary>The seller is picking and packing. Paid orders can be refunded; unpaid cash-on-delivery ones cancelled.</summary>
internal sealed class PreparingState : OrderState
{
    public override OrderStatus Status => OrderStatus.Preparing;

    protected override bool Accepts(OrderAction action, Order order) => action switch
    {
        OrderAction.Dispatch => true,
        OrderAction.Refund => !order.PaysOnDelivery,
        OrderAction.Cancel => order.PaysOnDelivery,
        _ => false,
    };

    public override Result Dispatch(Order order, DateTimeOffset now) => order.MoveTo(OrderStatus.OutForDelivery, now);

    public override Result Refund(Order order, string reason, DateTimeOffset now) =>
        order.PaysOnDelivery
            ? Error.BusinessRule("ordering.nothing_to_refund", "Nothing was paid yet; cancel the order instead.")
            : order.MoveTo(OrderStatus.Refunded, now, reason);

    public override Result Cancel(Order order, string reason, DateTimeOffset now) =>
        order.PaysOnDelivery
            ? order.MoveTo(OrderStatus.Cancelled, now, reason)
            : Error.BusinessRule("ordering.already_paid", "The order was paid; refund it instead of cancelling.");
}

/// <summary>With the motoqueiro, the txopela or the carrier.</summary>
internal sealed class OutForDeliveryState : OrderState
{
    public override OrderStatus Status => OrderStatus.OutForDelivery;

    protected override bool Accepts(OrderAction action, Order order) => action == OrderAction.Deliver;

    public override Result Deliver(Order order, DateTimeOffset now) => order.MarkDelivered(now);
}

internal sealed class DeliveredState : OrderState
{
    public override OrderStatus Status => OrderStatus.Delivered;

    protected override bool Accepts(OrderAction action, Order order) => action == OrderAction.Refund;

    public override Result Refund(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.Refunded, now, reason);
}

internal sealed class CancelledState : OrderState
{
    public override OrderStatus Status => OrderStatus.Cancelled;
}

internal sealed class RefundedState : OrderState
{
    public override OrderStatus Status => OrderStatus.Refunded;
}
