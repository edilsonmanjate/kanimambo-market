using Kanimambo.Payments.Contracts;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Payments;

/// <summary>Observer: Ordering reacts to the Payments module's outcomes (it never calls the gateways itself).</summary>
public sealed class PaymentOutcomeSubscribers(ISender sender) :
    INotificationHandler<PaymentConfirmedIntegrationEvent>,
    INotificationHandler<PaymentFailedIntegrationEvent>
{
    public async ValueTask Handle(PaymentConfirmedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Ensure(await sender.Send(new ApplyPaymentConfirmation(notification.EventId, notification.OrderId, notification.Attempt, notification.Reference), cancellationToken));
    }

    public async ValueTask Handle(PaymentFailedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Ensure(await sender.Send(new ApplyPaymentFailure(notification.EventId, notification.OrderId, notification.Attempt, notification.Reason), cancellationToken));
    }

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Could not apply the payment outcome: {result.Error.Message}");
        }
    }
}
