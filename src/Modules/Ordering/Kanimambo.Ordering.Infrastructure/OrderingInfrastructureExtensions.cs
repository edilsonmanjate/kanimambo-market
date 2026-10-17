using Kanimambo.Ordering.Application;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kanimambo.Ordering.Infrastructure;

public sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

    public int MaxOrdersPerPhonePerDay { get; set; } = 5;

    public decimal MaxValuePerPhonePerDayMzn { get; set; } = 100_000m;

    public InterimShippingOptions InterimShipping { get; set; } = new();
}

/// <summary>Flat delivery fee per city until the Shipping module (Phase 5) quotes real costs.</summary>
public sealed class InterimShippingOptions
{
    public decimal DefaultFeeMzn { get; set; } = 450m;

    public Dictionary<string, decimal> FeesByCityMzn { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class InterimShippingQuoter(IOptions<OrderingOptions> options) : IShippingQuoter
{
    public Task<Result<Money>> QuoteAsync(string deliveryCity, decimal weightKg, IReadOnlyCollection<Guid> sellerIds, CancellationToken cancellationToken)
    {
        var shipping = options.Value.InterimShipping;
        var fee = shipping.FeesByCityMzn.TryGetValue(deliveryCity, out var cityFee) ? cityFee : shipping.DefaultFeeMzn;
        return Task.FromResult(Result.Success(Money.Mzn(fee)));
    }
}

public static class OrderingInfrastructureExtensions
{
    public static IHostApplicationBuilder AddOrderingModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Ordering publishes integration events: domain events go to its outbox in the same transaction.
        builder.AddModuleDbContext<OrderingDbContext>(OrderingModule.Name, OrderingDbContext.Schema, publishesEvents: true);
        builder.Services.AddKeyedSingleton<IIntegrationEventMapper, OrderingIntegrationEventMapper>(typeof(OrderingDbContext));

        builder.Services.AddOptions<OrderingOptions>().Bind(builder.Configuration.GetSection(OrderingOptions.SectionName));
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OrderingOptions>>().Value;
            return new AntiFraudSettings(options.MaxOrdersPerPhonePerDay, options.MaxValuePerPhonePerDayMzn);
        });

        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<IDraftOrderRepository, DraftOrderRepository>();
        builder.Services.AddScoped<IDraftOrderHistory, DraftOrderHistory>();
        builder.Services.AddScoped<IOrderNumbers, OrderNumbers>();
        builder.Services.AddScoped<IShippingQuoter, InterimShippingQuoter>();
        builder.Services.AddOrderingApplication();
        return builder;
    }
}
