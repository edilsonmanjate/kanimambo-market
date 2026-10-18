using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

/// <summary>
/// Insufficient balance, wrong PIN, timeout... the customer may try again. A confirmation that arrives after a
/// timeout is still accepted: the operator took the money, so the order is paid.
/// </summary>
internal sealed class PaymentFailedState : OrderState
{
    public override OrderStatus Status => OrderStatus.PaymentFailed;

    protected override bool Accepts(OrderAction action, Order order) =>
        action is OrderAction.RetryPayment or OrderAction.Cancel or OrderAction.ConfirmPayment;

    public override Result ConfirmPayment(Order order, string reference, DateTimeOffset now) => order.MarkPaid(reference, now);

    public override Result RetryPayment(Order order, DateTimeOffset now) => order.MoveTo(OrderStatus.AwaitingPayment, now);

    public override Result Cancel(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.Cancelled, now, reason);
}
