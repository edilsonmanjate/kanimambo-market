using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class SuspendSellerHandler(CallLog log) : IRequestHandler<SuspendSeller, Result>
{
    public ValueTask<Result> Handle(SuspendSeller request, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult<Result>(Error.BusinessRule("catalog.seller_has_open_orders", "Seller has open orders."));
    }
}
