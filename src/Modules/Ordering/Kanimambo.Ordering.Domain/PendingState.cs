using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

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
