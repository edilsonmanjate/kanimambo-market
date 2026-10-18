namespace Kanimambo.Ordering.Domain;

internal sealed class CancelledState : OrderState
{
    public override OrderStatus Status => OrderStatus.Cancelled;
}
