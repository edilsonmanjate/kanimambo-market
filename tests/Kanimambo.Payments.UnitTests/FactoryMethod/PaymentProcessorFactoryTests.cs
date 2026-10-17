using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Application.Processing;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using static Kanimambo.Payments.UnitTests.TestPayments;

namespace Kanimambo.Payments.UnitTests.FactoryMethod;

public sealed class PaymentProcessorFactoryTests
{
    private static readonly PaymentSettings Settings = new(TimeSpan.FromSeconds(60), new Uri("https://api.kanimambo.test/"));

    private readonly IMobileMoneyGatewayProvider _gateways = Substitute.For<IMobileMoneyGatewayProvider>();
    private readonly ICardAcquirer _acquirer = Substitute.For<ICardAcquirer>();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Dictionary<MobileOperator, IMobileMoneyGateway> _byOperator = [];

    public PaymentProcessorFactoryTests()
    {
        foreach (var op in Enum.GetValues<MobileOperator>())
        {
            var gateway = Substitute.For<IMobileMoneyGateway>();
            gateway.Operator.Returns(op);
            _byOperator[op] = gateway;
            _gateways.For(op).Returns(gateway);
        }
    }

    private PaymentProcessorFactory Factory => new(new OperatorDirectory(new Dictionary<MobileOperator, IReadOnlyCollection<string>>
    {
        [MobileOperator.MPesa] = ["84", "85"],
        [MobileOperator.EMola] = ["86", "87"],
        [MobileOperator.MKesh] = ["82", "83"],
    }), _gateways, _acquirer, Settings, _clock);

    [Theory]
    [InlineData(PaymentMethod.MPesa, "258841234567", "MPesa")]
    [InlineData(PaymentMethod.MPesa, "258851234567", "MPesa")]
    [InlineData(PaymentMethod.EMola, "258861234567", "EMola")]
    [InlineData(PaymentMethod.EMola, "258871234567", "EMola")]
    [InlineData(PaymentMethod.MKesh, "258821234567", "MKesh")]
    [InlineData(PaymentMethod.MKesh, "258831234567", "MKesh")]
    public void Wallet_payments_get_the_processor_of_the_phones_operator(PaymentMethod method, string phone, string channel) =>
        Factory.CreateFor(method, phone).Value.Channel.ShouldBe(channel);

    [Fact]
    public void An_unknown_prefix_is_refused()
    {
        var error = Factory.CreateFor(PaymentMethod.MPesa, "258891234567").Error.ShouldBeOfType<GatewayError>();

        error.Reason.ShouldBe(PaymentFailureReason.UnknownOperator);
        error.Code.ShouldBe("payments.unknown_operator");
        error.NativeCode.ShouldBe("89");
    }

    [Theory]
    [InlineData("841234567")]
    [InlineData("258-84-123456")]
    [InlineData("351841234567")]
    public void Malformed_numbers_are_refused(string phone) =>
        Factory.CreateFor(PaymentMethod.MPesa, phone).Error.ShouldBeOfType<GatewayError>().Reason.ShouldBe(PaymentFailureReason.InvalidNumber);

    [Fact]
    public void A_wallet_that_does_not_match_the_phone_is_refused()
    {
        var error = Factory.CreateFor(PaymentMethod.EMola, "258841234567").Error.ShouldBeOfType<GatewayError>();

        error.Reason.ShouldBe(PaymentFailureReason.WalletMismatch);
        error.Message.ShouldContain("MPesa number");
    }

    [Fact]
    public void Cards_get_the_card_processor_whatever_the_phone() =>
        Factory.CreateFor(PaymentMethod.Card, "258891234567").Value.Channel.ShouldBe("Card");

    [Theory]
    [InlineData(PaymentMethod.CashOnDelivery)]
    [InlineData(PaymentMethod.None)]
    public void Methods_without_a_collection_step_are_unsupported(PaymentMethod method) =>
        Factory.CreateFor(method, "258841234567").Error.ShouldBeOfType<GatewayError>().Reason.ShouldBe(PaymentFailureReason.UnsupportedMethod);

    [Fact]
    public async Task The_mobile_money_processor_waits_for_the_pin_with_a_callback_to_this_api()
    {
        var gateway = _byOperator[MobileOperator.EMola];
        gateway.RequestPaymentAsync(Arg.Any<MobileMoneyRequest>(), Arg.Any<CancellationToken>()).Returns(new GatewayAcceptance("EM-1"));
        var payment = Initiated(PaymentMethod.EMola, "258861234567");

        await Factory.CreateFor(PaymentMethod.EMola, payment.Phone).Value.StartAsync(payment, CancellationToken.None);

        payment.Status.ShouldBe(PaymentStatus.AwaitingCustomerPin);
        payment.Operator.ShouldBe(MobileOperator.EMola);
        payment.OperatorTransactionId.ShouldBe("EM-1");
        payment.PinExpiresAt.ShouldBe(Now.AddSeconds(60));
        await gateway.Received(1).RequestPaymentAsync(
            Arg.Is<MobileMoneyRequest>(r => r.PaymentId == payment.Id
                && r.CallbackUrl == new Uri("https://api.kanimambo.test/api/payments/callbacks/emola")
                && r.Amount == payment.Amount),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_operator_refusal_fails_the_payment_with_the_native_code()
    {
        _byOperator[MobileOperator.MPesa].RequestPaymentAsync(Arg.Any<MobileMoneyRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayError(PaymentFailureReason.DuplicateTransaction, "INS-5", "Duplicate"));
        var payment = Initiated();

        await Factory.CreateFor(PaymentMethod.MPesa, payment.Phone).Value.StartAsync(payment, CancellationToken.None);

        payment.Status.ShouldBe(PaymentStatus.Failed);
        payment.FailureReason.ShouldBe(PaymentFailureReason.DuplicateTransaction);
        payment.NativeCode.ShouldBe("INS-5");
    }

    [Theory]
    [InlineData(true, PaymentStatus.Confirmed)]
    [InlineData(false, PaymentStatus.Failed)]
    public async Task Card_payments_settle_synchronously(bool approved, PaymentStatus expected)
    {
        _acquirer.ChargeAsync(Arg.Any<Guid>(), Arg.Any<Money>(), Arg.Any<CancellationToken>())
            .Returns(approved ? Result.Success("SIMO-42") : Result<string>.Failure(Error.Conflict("card.declined", "Declined")));
        var payment = Initiated(PaymentMethod.Card);

        await Factory.CreateFor(PaymentMethod.Card, payment.Phone).Value.StartAsync(payment, CancellationToken.None);

        payment.Status.ShouldBe(expected);
    }
}
