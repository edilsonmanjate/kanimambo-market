using Kanimambo.Ordering.Domain;
using Kanimambo.Payments.Contracts;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Ordering.Application.Payments;

public sealed record ApplyPaymentConfirmation(Guid IdempotencyKey, Guid OrderId, int Attempt, string Reference) : IIdempotentCommand;

public sealed record ApplyPaymentFailure(Guid IdempotencyKey, Guid OrderId, int Attempt, string Reason) : IIdempotentCommand;

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

public sealed partial class PaymentOutcomeHandlers(IOrderRepository orders, TimeProvider timeProvider, ILogger<PaymentOutcomeHandlers> logger) :
    IRequestHandler<ApplyPaymentConfirmation, Result>,
    IRequestHandler<ApplyPaymentFailure, Result>
{
    public async ValueTask<Result> Handle(ApplyPaymentConfirmation request, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(new OrderId(request.OrderId), cancellationToken);
        if (order is null)
        {
            return OrderingErrors.OrderNotFound(request.OrderId);
        }

        // Money was taken, whatever attempt it belongs to: accept it if the state allows (including a late
        // confirmation after a timeout); otherwise it needs a human (e.g. the order was cancelled meanwhile).
        var confirmed = order.ConfirmPayment(request.Reference, timeProvider.GetUtcNow());
        if (confirmed.IsFailure)
        {
            LogNeedsReconciliation(logger, order.DisplayNumber, order.Status.ToString(), request.Reference);
        }

        return Result.Success();
    }

    public async ValueTask<Result> Handle(ApplyPaymentFailure request, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(new OrderId(request.OrderId), cancellationToken);
        if (order is null)
        {
            return OrderingErrors.OrderNotFound(request.OrderId);
        }

        // A failure of an older attempt must not fail the retry the customer is doing now.
        if (request.Attempt == order.PaymentAttempts && order.Status == OrderStatus.AwaitingPayment)
        {
            order.FailPayment(request.Reason, timeProvider.GetUtcNow());
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment {Reference} for order {Order} arrived while it is {Status}: manual reconciliation needed")]
    private static partial void LogNeedsReconciliation(ILogger logger, string order, string status, string reference);
}
