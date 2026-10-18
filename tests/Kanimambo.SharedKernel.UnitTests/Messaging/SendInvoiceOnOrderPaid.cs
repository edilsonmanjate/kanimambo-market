using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class SendInvoiceOnOrderPaid(CallLog log) : INotificationHandler<OrderPaid>
{
    public ValueTask Handle(OrderPaid notification, CancellationToken cancellationToken)
    {
        log.Add("invoice");
        return ValueTask.CompletedTask;
    }
}
