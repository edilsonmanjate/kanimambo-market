using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Contracts;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Ordering.Application.Shipping;

public sealed record ApplyDelivery(Guid IdempotencyKey, Guid OrderId) : IIdempotentCommand;

/// <summary>Observer: when Shipping reports every parcel delivered, the order is delivered (and a cash order paid).</summary>
public sealed class DeliveryOutcomeSubscriber(ISender sender) : INotificationHandler<OrderDeliveredIntegrationEvent>
{
    public async ValueTask Handle(OrderDeliveredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var applied = await sender.Send(new ApplyDelivery(notification.EventId, notification.OrderId), cancellationToken);
        if (applied.IsFailure)
        {
            throw new InvalidOperationException($"Could not apply the delivery of order {notification.OrderNumber}: {applied.Error.Message}");
        }
    }
}

public sealed partial class DeliveryOutcomeHandler(IOrderRepository orders, TimeProvider timeProvider, ILogger<DeliveryOutcomeHandler> logger)
    : IRequestHandler<ApplyDelivery, Result>
{
    public async ValueTask<Result> Handle(ApplyDelivery request, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(new OrderId(request.OrderId), cancellationToken);
        if (order is null)
        {
            return OrderingErrors.OrderNotFound(request.OrderId);
        }

        // Already delivered by hand in the back-office, or refunded meanwhile: nothing to do, but worth a log line.
        if (order.Deliver(timeProvider.GetUtcNow()).IsFailure)
        {
            LogIgnored(logger, order.Number, order.Status);
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Courier delivered order KM-{OrderNumber} while it is {Status}; nothing to change")]
    private static partial void LogIgnored(ILogger logger, long orderNumber, OrderStatus status);
}
