using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Domain;

/// <summary>
/// State pattern for the mobile-money lifecycle:
/// <c>Initiated → AwaitingCustomerPin → Confirmed / Failed / TimedOut</c>, plus <c>TimedOut → Confirmed</c>
/// for a confirmation that arrives late (the operator did take the money). Everything else is refused.
/// </summary>
public abstract class PaymentState
{
    private static readonly Dictionary<PaymentStatus, PaymentState> States = new PaymentState[]
    {
        new InitiatedState(), new AwaitingCustomerPinState(), new ConfirmedState(), new FailedState(), new TimedOutState(),
    }.ToDictionary(s => s.Status);

    public abstract PaymentStatus Status { get; }

    public static PaymentState Of(PaymentStatus status) => States[status];

    public virtual Result AwaitCustomerPin(Payment payment, MobileOperator? mobileOperator, string operatorTransactionId, DateTimeOffset pinExpiresAt, DateTimeOffset now) =>
        Refuse("wait for the customer's PIN");

    public virtual Result Confirm(Payment payment, string operatorTransactionId, DateTimeOffset now) => Refuse("be confirmed");

    public virtual Result Fail(Payment payment, PaymentFailureReason reason, string? nativeCode, string detail, DateTimeOffset now) => Refuse("fail");

    public virtual Result TimeOut(Payment payment, DateTimeOffset now) => Refuse("time out");

    protected Result Refuse(string what) =>
        Error.Conflict("payments.invalid_transition", $"A payment that is {Status} cannot {what}.");
}

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

internal sealed class ConfirmedState : PaymentState
{
    public override PaymentStatus Status => PaymentStatus.Confirmed;
}

internal sealed class FailedState : PaymentState
{
    public override PaymentStatus Status => PaymentStatus.Failed;
}
