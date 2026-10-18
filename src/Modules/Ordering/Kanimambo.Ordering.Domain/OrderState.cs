using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

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
