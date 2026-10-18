namespace Kanimambo.Payments.Domain;

internal sealed class FailedState : PaymentState
{
    public override PaymentStatus Status => PaymentStatus.Failed;
}
