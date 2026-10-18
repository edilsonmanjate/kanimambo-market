using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

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
