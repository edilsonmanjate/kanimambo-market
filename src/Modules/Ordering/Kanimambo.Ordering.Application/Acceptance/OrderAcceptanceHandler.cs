using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

/// <summary>
/// Chain of Responsibility: one acceptance check. It either stops the chain with a typed error, or passes
/// the order to the next check. Checks don't know each other or their position.
/// </summary>
public abstract class OrderAcceptanceHandler
{
    private OrderAcceptanceHandler? _next;

    public string Name => GetType().Name;

    public async Task<Result> HandleAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        var result = await CheckAsync(context, cancellationToken);
        return result.IsFailure || _next is null ? result : await _next.HandleAsync(context, cancellationToken);
    }

    internal OrderAcceptanceHandler LinkTo(OrderAcceptanceHandler next) => _next = next;

    protected abstract Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken);
}
