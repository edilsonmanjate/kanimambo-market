using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class PingHandler(CallLog log) : IRequestHandler<Ping, Result<string>>
{
    public ValueTask<Result<string>> Handle(Ping request, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult<Result<string>>($"pong:{request.Message}");
    }
}
