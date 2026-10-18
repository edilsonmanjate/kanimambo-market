using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Ordering.Application.Acceptance;

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
