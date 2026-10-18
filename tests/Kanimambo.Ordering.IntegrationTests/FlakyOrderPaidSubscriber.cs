using Kanimambo.Ordering.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.IntegrationTests;

/// <summary>Stands in for a not-yet-built subscriber (e.g. invoicing) that can fail on demand.</summary>
public sealed class FlakyOrderPaidSubscriber(FailurePlan plan) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public ValueTask Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        plan.Calls.AddOrUpdate(notification.OrderId, 1, (_, n) => n + 1);

        if (plan.FailAlways.ContainsKey(notification.OrderId)
            || (plan.FailTimes.TryGetValue(notification.OrderId, out var left) && left > 0 && plan.FailTimes.TryUpdate(notification.OrderId, left - 1, left)))
        {
            throw new InvalidOperationException($"Simulated outage for order {notification.OrderNumber}");
        }

        return ValueTask.CompletedTask;
    }
}
