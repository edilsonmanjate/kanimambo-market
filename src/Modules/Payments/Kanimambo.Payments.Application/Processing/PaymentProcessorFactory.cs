using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Application.Processing;

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
