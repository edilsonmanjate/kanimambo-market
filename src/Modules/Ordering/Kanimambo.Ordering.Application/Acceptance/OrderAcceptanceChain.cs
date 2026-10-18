using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

internal sealed class OrderAcceptanceChain : IOrderAcceptanceChain
{
    private readonly OrderAcceptanceHandler? _first;

    public OrderAcceptanceChain(IReadOnlyList<OrderAcceptanceHandler> handlers)
    {
        Steps = handlers.Select(h => h.Name).ToList();
        _first = handlers.Count == 0 ? null : handlers[0];
        for (var i = 0; i + 1 < handlers.Count; i++)
        {
            handlers[i].LinkTo(handlers[i + 1]);
        }
    }

    public IReadOnlyList<string> Steps { get; }

    public Task<Result> AcceptAsync(OrderAcceptanceContext context, CancellationToken cancellationToken) =>
        _first?.HandleAsync(context, cancellationToken) ?? Task.FromResult(Result.Success());
}
