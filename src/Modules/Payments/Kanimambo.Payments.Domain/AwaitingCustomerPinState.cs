using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Domain;

internal sealed class AwaitingCustomerPinState : PaymentState
{
    public override PaymentStatus Status => PaymentStatus.AwaitingCustomerPin;

    public override Result Confirm(Payment payment, string operatorTransactionId, DateTimeOffset now)
    {
        payment.RecordConfirmed(operatorTransactionId, now);
        return Result.Success();
    }

    public override Result Fail(Payment payment, PaymentFailureReason reason, string? nativeCode, string detail, DateTimeOffset now)
    {
        payment.RecordFailed(reason, nativeCode, detail, now);
        return Result.Success();
    }

    public override Result TimeOut(Payment payment, DateTimeOffset now)
    {
        payment.RecordTimedOut(now);
        return Result.Success();
    }
}
