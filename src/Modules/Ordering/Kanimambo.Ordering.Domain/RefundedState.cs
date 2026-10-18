namespace Kanimambo.Ordering.Domain;

internal sealed class RefundedState : OrderState
{
    public override OrderStatus Status => OrderStatus.Refunded;
}
