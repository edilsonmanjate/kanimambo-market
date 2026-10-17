using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Payments.UnitTests;

internal static class TestPayments
{
    public static readonly DateTimeOffset Now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    public static Payment Initiated(PaymentMethod method = PaymentMethod.MPesa, string phone = "258841234567", decimal amount = 1_234.50m) =>
        Payment.Start(Guid.NewGuid(), 1001, 1, method, phone, Money.Mzn(amount), Now);

    /// <summary>A payment driven into the given status through its public API only, with its events cleared.</summary>
    public static Payment In(PaymentStatus status)
    {
        var payment = Initiated();
        if (status is PaymentStatus.AwaitingCustomerPin or PaymentStatus.Confirmed or PaymentStatus.TimedOut)
        {
            payment.AwaitCustomerPin(MobileOperator.MPesa, "MP123", Now.AddMinutes(1), Now);
        }

        _ = status switch
        {
            PaymentStatus.Confirmed => payment.Confirm("MP123", Now),
            PaymentStatus.Failed => payment.Fail(PaymentFailureReason.InsufficientBalance, "INS-6", "No balance", Now),
            PaymentStatus.TimedOut => payment.TimeOut(Now.AddMinutes(1)),
            _ => null,
        };

        payment.Status.ShouldBe(status);
        payment.ClearDomainEvents();
        return payment;
    }
}
