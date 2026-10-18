using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class ConfirmMobileMoneyPaymentHandler(CallLog log) : IRequestHandler<ConfirmMobileMoneyPayment, Result>
{
    public ValueTask<Result> Handle(ConfirmMobileMoneyPayment request, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult(request.Succeeds
            ? Result.Success()
            : Result.Failure(Error.Failure("payments.gateway_error", "Gateway error.")));
    }
}
