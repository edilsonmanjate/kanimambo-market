using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Domain;

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
