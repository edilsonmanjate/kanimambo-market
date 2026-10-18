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
