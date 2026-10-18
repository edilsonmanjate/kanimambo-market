using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Ordering.Application.Shipping;

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
