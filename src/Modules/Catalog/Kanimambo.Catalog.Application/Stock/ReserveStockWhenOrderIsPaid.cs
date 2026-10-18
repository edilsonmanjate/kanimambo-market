using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Catalog.Application.Stock;

/// <summary>Observer: Catalog subscribes to Ordering's event without Ordering knowing about it.</summary>
public sealed class ReserveStockWhenOrderIsPaid(ISender sender) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async ValueTask Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var result = await sender.Send(new ReserveStock(notification.EventId, notification.OrderNumber, notification.Lines), cancellationToken);
        if (result.IsFailure)
        {
            // Surfaces to the outbox, which records the failure and retries this subscriber only.
            throw new InvalidOperationException($"Stock reservation for order {notification.OrderNumber} failed: {result.Error.Message}");
        }
    }
}
