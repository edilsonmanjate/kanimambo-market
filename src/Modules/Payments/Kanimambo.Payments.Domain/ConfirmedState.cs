namespace Kanimambo.Payments.Domain;

internal sealed class ConfirmedState : PaymentState
{
    public override PaymentStatus Status => PaymentStatus.Confirmed;
}
