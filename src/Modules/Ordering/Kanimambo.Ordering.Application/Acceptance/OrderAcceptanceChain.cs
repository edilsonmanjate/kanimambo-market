using Kanimambo.Catalog.Contracts;
using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Ordering.Application.Acceptance;

public sealed record AcceptanceLine(Guid ProductId, Guid SellerId, string Sku, int Quantity, int AvailableStock, Money LineTotal);

/// <summary>Everything the acceptance checks look at, gathered once by checkout.</summary>
public sealed record OrderAcceptanceContext(
    OrderCustomer Customer,
    string DeliveryCity,
    PaymentMethod PaymentMethod,
    IReadOnlyList<AcceptanceLine> Lines,
    IReadOnlyDictionary<Guid, SellerSnapshot> Sellers,
    Money Total);

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

public interface IOrderAcceptanceChain
{
    IReadOnlyList<string> Steps { get; }

    Task<Result> AcceptAsync(OrderAcceptanceContext context, CancellationToken cancellationToken);
}

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

/// <summary>Declares the checks and their order; the order is the registration order.</summary>
public sealed class OrderAcceptanceChainBuilder
{
    private readonly List<Type> _steps = [];

    internal IReadOnlyList<Type> Steps => _steps;

    public OrderAcceptanceChainBuilder Then<THandler>()
        where THandler : OrderAcceptanceHandler
    {
        _steps.Add(typeof(THandler));
        return this;
    }
}

public static class OrderAcceptanceChainServiceCollectionExtensions
{
    public static IServiceCollection AddOrderAcceptanceChain(this IServiceCollection services, Action<OrderAcceptanceChainBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new OrderAcceptanceChainBuilder();
        configure(builder);

        foreach (var step in builder.Steps)
        {
            services.AddScoped(step);
        }

        // Handlers are linked per scope (they hold a "next" reference), so each request gets its own chain.
        services.AddScoped<IOrderAcceptanceChain>(sp =>
            new OrderAcceptanceChain([.. builder.Steps.Select(t => (OrderAcceptanceHandler)sp.GetRequiredService(t))]));
        return services;
    }
}
