using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

/// <summary>Observer: a paid order gets its invoices (Ordering does not know Invoicing exists).</summary>
public sealed class InvoiceWhenOrderPaid(ISender sender) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async ValueTask Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var issued = await sender.Send(new IssueInvoicesForOrder(notification.EventId, notification.OrderId, notification.OrderNumber, notification.CustomerId,
            notification.CustomerType, notification.CustomerNuit, notification.CustomerPhone, notification.DiscountMzn, notification.ShippingFeeMzn,
            notification.DeliveryMethod, notification.Lines), cancellationToken);
        if (issued.IsFailure)
        {
            throw new InvalidOperationException($"Could not invoice order {notification.OrderNumber}: {issued.Error.Message}");
        }
    }
}
