using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Domain;

/// <summary>Mobile-money operators: M-Pesa (Vodacom), e-Mola (Movitel), mKesh (Tmcel).</summary>
public enum MobileOperator
{
    MPesa,
    EMola,
    MKesh,
}

/// <summary>Why a payment did not happen — one vocabulary, whatever the operator's native code was.</summary>
public enum PaymentFailureReason
{
    InsufficientBalance,
    DuplicateTransaction,
    InvalidNumber,
    Timeout,
    Rejected,
    GatewayUnavailable,
    UnknownOperator,
    WalletMismatch,
    UnsupportedMethod,
}

public enum PaymentStatus
{
    Initiated,
    AwaitingCustomerPin,
    Confirmed,
    Failed,
    TimedOut,
}

public sealed record PaymentConfirmed(Guid PaymentId, Guid OrderId, int Attempt, string Reference, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

public sealed record PaymentFailed(Guid PaymentId, Guid OrderId, int Attempt, PaymentFailureReason Reason, string Detail, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

public sealed record PaymentTransition(PaymentStatus From, PaymentStatus To, DateTimeOffset At, string? Detail);

/// <summary>
/// One attempt to collect the money for an order. Mobile money is asynchronous: the operator accepts the
/// request, the customer confirms with their PIN on the phone, and the outcome arrives later by callback —
/// or not at all, in which case the payment times out. Status-dependent behaviour lives in <see cref="PaymentState"/>.
/// </summary>
public sealed class Payment : AggregateRoot<Guid>
{
    private readonly List<PaymentTransition> _history = [];
    private decimal _amountMzn;

    private Payment()
    {
    }

    private Payment(Guid id, Guid orderId, long orderNumber, int attempt, PaymentMethod method, string phone, Money amount, DateTimeOffset now)
        : base(id)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        Attempt = attempt;
        Method = method;
        Phone = phone;
        _amountMzn = amount.Amount;
        Status = PaymentStatus.Initiated;
        CreatedAt = UpdatedAt = now;
    }

    public Guid OrderId { get; private set; }

    public long OrderNumber { get; private set; }

    public int Attempt { get; private set; }

    public PaymentMethod Method { get; private set; }

    public MobileOperator? Operator { get; private set; }

    public string Phone { get; private set; } = string.Empty;

    public Money Amount => Money.Mzn(_amountMzn);

    public PaymentStatus Status { get; private set; }

    /// <summary>The operator's own transaction id, once it accepted the request.</summary>
    public string? OperatorTransactionId { get; private set; }

    public PaymentFailureReason? FailureReason { get; private set; }

    /// <summary>The operator's native code (e.g. <c>INS-6</c>), kept for support.</summary>
    public string? NativeCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>When an unanswered PIN request is considered timed out.</summary>
    public DateTimeOffset? PinExpiresAt { get; private set; }

    public IReadOnlyList<PaymentTransition> History => _history.AsReadOnly();

    public PaymentState State => PaymentState.Of(Status);

    public static Payment Start(Guid orderId, long orderNumber, int attempt, PaymentMethod method, string phone, Money amount, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(amount);
        return new Payment(Guid.NewGuid(), orderId, orderNumber, attempt, method, phone, amount, now);
    }

    public Result AwaitCustomerPin(MobileOperator? mobileOperator, string operatorTransactionId, DateTimeOffset pinExpiresAt, DateTimeOffset now) =>
        State.AwaitCustomerPin(this, mobileOperator, operatorTransactionId, pinExpiresAt, now);

    public Result Confirm(string operatorTransactionId, DateTimeOffset now) => State.Confirm(this, operatorTransactionId, now);

    public Result Fail(PaymentFailureReason reason, string? nativeCode, string detail, DateTimeOffset now) => State.Fail(this, reason, nativeCode, detail, now);

    public Result TimeOut(DateTimeOffset now) => State.TimeOut(this, now);

    internal void RecordAwaiting(MobileOperator? mobileOperator, string operatorTransactionId, DateTimeOffset pinExpiresAt, DateTimeOffset now)
    {
        Operator = mobileOperator;
        OperatorTransactionId = operatorTransactionId;
        PinExpiresAt = pinExpiresAt;
        Move(PaymentStatus.AwaitingCustomerPin, now, operatorTransactionId);
    }

    internal void RecordConfirmed(string operatorTransactionId, DateTimeOffset now)
    {
        OperatorTransactionId = string.IsNullOrWhiteSpace(operatorTransactionId) ? OperatorTransactionId : operatorTransactionId;
        Move(PaymentStatus.Confirmed, now, OperatorTransactionId);
        Raise(new PaymentConfirmed(Id, OrderId, Attempt, OperatorTransactionId ?? Id.ToString(), now));
    }

    internal void RecordFailed(PaymentFailureReason reason, string? nativeCode, string detail, DateTimeOffset now)
    {
        FailureReason = reason;
        NativeCode = nativeCode;
        Move(PaymentStatus.Failed, now, detail);
        Raise(new PaymentFailed(Id, OrderId, Attempt, reason, detail, now));
    }

    internal void RecordTimedOut(DateTimeOffset now)
    {
        FailureReason = PaymentFailureReason.Timeout;
        Move(PaymentStatus.TimedOut, now, "No confirmation from the customer in time.");
        Raise(new PaymentFailed(Id, OrderId, Attempt, PaymentFailureReason.Timeout, "The customer did not confirm the payment in time.", now));
    }

    private void Move(PaymentStatus next, DateTimeOffset now, string? detail)
    {
        _history.Add(new PaymentTransition(Status, next, now, detail));
        Status = next;
        UpdatedAt = now;
    }
}
