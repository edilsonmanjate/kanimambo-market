using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

/// <summary>With the motoqueiro, the txopela or the carrier.</summary>
internal sealed class OutForDeliveryState : OrderState
{
    public override OrderStatus Status => OrderStatus.OutForDelivery;

    protected override bool Accepts(OrderAction action, Order order) => action == OrderAction.Deliver;

    public override Result Deliver(Order order, DateTimeOffset now) => order.MarkDelivered(now);
}
