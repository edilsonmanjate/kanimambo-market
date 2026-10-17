using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;
using static Kanimambo.Payments.UnitTests.TestPayments;
using S = Kanimambo.Payments.Domain.PaymentStatus;

namespace Kanimambo.Payments.UnitTests.State;

/// <summary>Exhaustive: every status × every action. Anything not in the table is refused and changes nothing.</summary>
public sealed class PaymentStateTests
{
    public enum Action
    {
        AwaitPin,
        Confirm,
        Fail,
        TimeOut,
    }

    private static readonly Dictionary<(S, Action), S> Allowed = new()
    {
        [(S.Initiated, Action.AwaitPin)] = S.AwaitingCustomerPin,
        [(S.Initiated, Action.Fail)] = S.Failed,
        [(S.AwaitingCustomerPin, Action.Confirm)] = S.Confirmed,
        [(S.AwaitingCustomerPin, Action.Fail)] = S.Failed,
        [(S.AwaitingCustomerPin, Action.TimeOut)] = S.TimedOut,
        [(S.TimedOut, Action.Confirm)] = S.Confirmed, // late callback: the money was taken
    };

    public static TheoryData<S, Action> AllCombinations()
    {
        var data = new TheoryData<S, Action>();
        foreach (var status in Enum.GetValues<S>())
        {
            foreach (var action in Enum.GetValues<Action>())
            {
                data.Add(status, action);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Every_status_accepts_exactly_its_transitions(S status, Action action)
    {
        var payment = In(status);
        var historyBefore = payment.History.Count;

        var result = Apply(payment, action);

        if (Allowed.TryGetValue((status, action), out var expected))
        {
            result.IsSuccess.ShouldBeTrue();
            payment.Status.ShouldBe(expected);
            payment.History[^1].From.ShouldBe(status);
            payment.History[^1].To.ShouldBe(expected);
        }
        else
        {
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("payments.invalid_transition");
            payment.Status.ShouldBe(status);
            payment.History.Count.ShouldBe(historyBefore);
            payment.DomainEvents.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Confirming_raises_PaymentConfirmed_with_the_operator_reference()
    {
        var payment = In(S.AwaitingCustomerPin);

        payment.Confirm("MP999", Now);

        var confirmed = payment.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<PaymentConfirmed>();
        confirmed.Reference.ShouldBe("MP999");
        confirmed.OrderId.ShouldBe(payment.OrderId);
        confirmed.Attempt.ShouldBe(payment.Attempt);
    }

    [Fact]
    public void Failing_keeps_the_native_code_and_raises_PaymentFailed()
    {
        var payment = In(S.AwaitingCustomerPin);

        payment.Fail(PaymentFailureReason.InsufficientBalance, "INS-6", "Saldo insuficiente", Now);

        payment.FailureReason.ShouldBe(PaymentFailureReason.InsufficientBalance);
        payment.NativeCode.ShouldBe("INS-6");
        payment.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<PaymentFailed>().Reason.ShouldBe(PaymentFailureReason.InsufficientBalance);
    }

    [Fact]
    public void Timing_out_is_reported_as_a_failure_with_reason_timeout()
    {
        var payment = In(S.AwaitingCustomerPin);

        payment.TimeOut(Now.AddMinutes(2));

        payment.FailureReason.ShouldBe(PaymentFailureReason.Timeout);
        payment.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<PaymentFailed>().Reason.ShouldBe(PaymentFailureReason.Timeout);
    }

    private static Result Apply(Payment payment, Action action) => action switch
    {
        Action.AwaitPin => payment.AwaitCustomerPin(MobileOperator.MPesa, "MP555", Now.AddMinutes(1), Now),
        Action.Confirm => payment.Confirm("MP555", Now),
        Action.Fail => payment.Fail(PaymentFailureReason.Rejected, "X", "Rejected", Now),
        Action.TimeOut => payment.TimeOut(Now),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}
