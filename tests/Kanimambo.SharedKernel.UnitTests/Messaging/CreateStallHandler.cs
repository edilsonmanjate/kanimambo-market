using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class CreateStallHandler(CallLog log) : IRequestHandler<CreateStall, Result<Guid>>
{
    public ValueTask<Result<Guid>> Handle(CreateStall request, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult<Result<Guid>>(Guid.CreateVersion7());
    }
}
