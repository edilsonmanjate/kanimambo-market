using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Application.Processing;

/// <summary>Timing and addressing for payments, from configuration.</summary>
public sealed record PaymentSettings(TimeSpan PinTimeout, Uri CallbackBaseAddress);

/// <summary>Which phone prefixes belong to which operator (configurable: numbering plans change).</summary>
public sealed class OperatorDirectory(IReadOnlyDictionary<MobileOperator, IReadOnlyCollection<string>> prefixes)
{
    public Result<MobileOperator> OperatorFor(string phone)
    {
        if (phone is not { Length: 12 } || !phone.StartsWith("258", StringComparison.Ordinal))
        {
            return new GatewayError(PaymentFailureReason.InvalidNumber, null, "Phone numbers use the 258XXXXXXXXX format.");
        }

        var prefix = phone.Substring(3, 2);
        foreach (var (mobileOperator, operatorPrefixes) in prefixes)
        {
            if (operatorPrefixes.Contains(prefix))
            {
                return mobileOperator;
            }
        }

        return new GatewayError(PaymentFailureReason.UnknownOperator, prefix,
            $"No mobile-money operator uses the prefix {prefix}. Use an M-Pesa, e-Mola or mKesh number.");
    }
}

/// <summary>Starts the collection of one payment through a specific channel.</summary>
public interface IPaymentProcessor
{
    string Channel { get; }

    /// <summary>Moves the payment forward (awaiting PIN, confirmed or failed); never throws for business outcomes.</summary>
    Task StartAsync(Payment payment, CancellationToken cancellationToken);
}

/// <summary>
/// Factory Method: creates the right processor for a payment — by the phone's operator prefix for mobile
/// wallets (84/85 M-Pesa, 86/87 e-Mola, 82/83 mKesh, from configuration), or by the chosen method for cards.
/// Callers never construct processors, so adding an operator or a method touches only this factory.
/// </summary>
public sealed class PaymentProcessorFactory(
    OperatorDirectory operators,
    IMobileMoneyGatewayProvider gateways,
    ICardAcquirer cardAcquirer,
    PaymentSettings settings,
    TimeProvider timeProvider)
{
    private static readonly Dictionary<PaymentMethod, MobileOperator> Wallets = new()
    {
        [PaymentMethod.MPesa] = MobileOperator.MPesa,
        [PaymentMethod.EMola] = MobileOperator.EMola,
        [PaymentMethod.MKesh] = MobileOperator.MKesh,
    };

    public Result<IPaymentProcessor> CreateFor(PaymentMethod method, string phone)
    {
        if (Wallets.TryGetValue(method, out var wallet))
        {
            var byPrefix = operators.OperatorFor(phone);
            if (byPrefix.IsFailure)
            {
                return byPrefix.Error;
            }

            if (byPrefix.Value != wallet)
            {
                return new GatewayError(PaymentFailureReason.WalletMismatch, null,
                    $"{phone} is a {byPrefix.Value} number; it cannot pay with {method}.");
            }

            return new MobileMoneyProcessor(gateways.For(wallet), settings, timeProvider);
        }

        return method switch
        {
            PaymentMethod.Card => new CardProcessor(cardAcquirer, timeProvider),
            PaymentMethod.CashOnDelivery => new GatewayError(PaymentFailureReason.UnsupportedMethod, null, "Cash is collected by the courier on delivery, not requested."),
            _ => new GatewayError(PaymentFailureReason.UnsupportedMethod, null, $"Payment method {method} is not supported."),
        };
    }
}

internal sealed class MobileMoneyProcessor(IMobileMoneyGateway gateway, PaymentSettings settings, TimeProvider timeProvider) : IPaymentProcessor
{
    public string Channel => gateway.Operator.ToString();

    public async Task StartAsync(Payment payment, CancellationToken cancellationToken)
    {
        var callback = new Uri(settings.CallbackBaseAddress, $"api/payments/callbacks/{gateway.Operator.ToString().ToLowerInvariant()}");
        var request = new MobileMoneyRequest(payment.Id, payment.Phone, payment.Amount, callback, $"Kanimambo encomenda KM-{payment.OrderNumber}");

        var accepted = await gateway.RequestPaymentAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (accepted.IsSuccess)
        {
            payment.AwaitCustomerPin(gateway.Operator, accepted.Value.OperatorTransactionId, now + settings.PinTimeout, now);
            return;
        }

        var error = accepted.Error as GatewayError ?? new GatewayError(PaymentFailureReason.Rejected, null, accepted.Error.Message);
        payment.Fail(error.Reason, error.NativeCode, error.Message, now);
    }
}

internal sealed class CardProcessor(ICardAcquirer acquirer, TimeProvider timeProvider) : IPaymentProcessor
{
    public string Channel => "Card";

    public async Task StartAsync(Payment payment, CancellationToken cancellationToken)
    {
        var charged = await acquirer.ChargeAsync(payment.Id, payment.Amount, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (charged.IsFailure)
        {
            payment.Fail(PaymentFailureReason.Rejected, null, charged.Error.Message, now);
            return;
        }

        // Card authorisations are synchronous: the "PIN" step is the card holder's verification at the terminal.
        payment.AwaitCustomerPin(null, charged.Value, now, now);
        payment.Confirm(charged.Value, now);
    }
}
