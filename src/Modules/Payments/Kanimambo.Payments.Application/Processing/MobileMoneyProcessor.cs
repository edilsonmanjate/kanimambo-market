using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;

namespace Kanimambo.Payments.Application.Processing;

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
