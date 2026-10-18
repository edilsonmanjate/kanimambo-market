using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;

namespace Kanimambo.Payments.Application.Processing;

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
