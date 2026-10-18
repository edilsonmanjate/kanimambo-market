using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Domain;

internal sealed class TimedOutState : PaymentState
{
    public override PaymentStatus Status => PaymentStatus.TimedOut;

    /// <summary>The callback came after we gave up — the money was still taken, so the payment counts.</summary>
    public override Result Confirm(Payment payment, string operatorTransactionId, DateTimeOffset now)
    {
        payment.RecordConfirmed(operatorTransactionId, now);
        return Result.Success();
    }
}
