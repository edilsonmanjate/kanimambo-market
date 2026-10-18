using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

internal sealed class PaidState : OrderState
{
    public override OrderStatus Status => OrderStatus.Paid;

    protected override bool Accepts(OrderAction action, Order order) => action is OrderAction.StartPreparing or OrderAction.Refund;

    public override Result StartPreparing(Order order, DateTimeOffset now) => order.MoveTo(OrderStatus.Preparing, now);

    public override Result Refund(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.Refunded, now, reason);
}
