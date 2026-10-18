using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

public interface IOrderAcceptanceChain
{
    IReadOnlyList<string> Steps { get; }

    Task<Result> AcceptAsync(OrderAcceptanceContext context, CancellationToken cancellationToken);
}
