using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class FailingOrderPaidHandler(CallLog log) : INotificationHandler<OrderPaid>
{
    public ValueTask Handle(OrderPaid notification, CancellationToken cancellationToken)
    {
        log.Add("failing");
        throw new InvalidOperationException("SMS gateway down");
    }
}
