using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Domain;

internal sealed class InitiatedState : PaymentState
{
    public override PaymentStatus Status => PaymentStatus.Initiated;

    public override Result AwaitCustomerPin(Payment payment, MobileOperator? mobileOperator, string operatorTransactionId, DateTimeOffset pinExpiresAt, DateTimeOffset now)
    {
        payment.RecordAwaiting(mobileOperator, operatorTransactionId, pinExpiresAt, now);
        return Result.Success();
    }

    /// <summary>Rejected before reaching the customer: unknown prefix, insufficient balance checked upfront, duplicate...</summary>
    public override Result Fail(Payment payment, PaymentFailureReason reason, string? nativeCode, string detail, DateTimeOffset now)
    {
        payment.RecordFailed(reason, nativeCode, detail, now);
        return Result.Success();
    }
}
