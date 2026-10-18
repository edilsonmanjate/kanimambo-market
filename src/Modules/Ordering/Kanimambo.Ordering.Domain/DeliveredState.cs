using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

internal sealed class DeliveredState : OrderState
{
    public override OrderStatus Status => OrderStatus.Delivered;

    protected override bool Accepts(OrderAction action, Order order) => action == OrderAction.Refund;

    public override Result Refund(Order order, string reason, DateTimeOffset now) => order.MoveTo(OrderStatus.Refunded, now, reason);
}
