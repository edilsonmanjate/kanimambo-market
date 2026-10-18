using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.Shipping.Application;
using Kanimambo.Shipping.Application.Couriers;
using Kanimambo.Shipping.Application.Pricing;
using Kanimambo.Shipping.Contracts;
using Kanimambo.Shipping.Infrastructure.Couriers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kanimambo.Shipping.Infrastructure;

public static class ShippingInfrastructureExtensions
{
    public static IHostApplicationBuilder AddShippingModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var section = builder.Configuration.GetSection(ShippingOptions.SectionName);
        var pricing = section.Get<ShippingOptions>() ?? new ShippingOptions();
        var connections = section.Get<ShippingConnectionOptions>() ?? new ShippingConnectionOptions();
        var services = builder.Services;

        builder.AddModuleDbContext<ShippingDbContext>(ShippingModule.Name, ShippingDbContext.Schema, publishesEvents: true);
        services.AddKeyedSingleton<IIntegrationEventMapper, ShippingIntegrationEventMapper>(typeof(ShippingDbContext));

        services.AddSingleton(pricing);
        services.AddSingleton(new ShippingSettings(connections.WebhookBaseAddress));
        services.AddScoped<IConsignmentRepository, ConsignmentRepository>();

        // Strategies: each one is registered once; ShippingStrategies finds them by method name.
        // A new delivery method is a new class, one line here and its configuration.
        services.AddSingleton<SameDayStrategy>();
        services.AddSingleton<CourierStrategy>();
        services.AddSingleton<PickupStrategy>();
        services.AddSingleton<IShippingStrategy>(sp => sp.GetRequiredService<SameDayStrategy>());
        services.AddSingleton<IShippingStrategy>(sp => sp.GetRequiredService<CourierStrategy>());
        services.AddSingleton<IShippingStrategy>(sp => sp.GetRequiredService<PickupStrategy>());
        services.AddSingleton<ShippingStrategies>();
        services.AddSingleton<IShippingQuoter, ShippingQuoter>();

        // Courier adapters. Bookings carry a deterministic reference, so the standard HTTP resilience retries are safe here.
        services.AddSingleton<ICourierGatewayProvider, KeyedCourierProvider>();
        AddCourier(services, TxovaAdapter.CourierName, connections, (http, o) => new TxovaAdapter(http, o));
        AddCourier(services, MbilaAdapter.CourierName, connections, (http, o) => new MbilaAdapter(http, o));
        return builder;
    }

    private static void AddCourier(
        IServiceCollection services,
        string name,
        ShippingConnectionOptions connections,
        Func<HttpClient, CourierConnectionOptions, ICourierGateway> createAdapter)
    {
        var options = connections.Couriers.GetValueOrDefault(name) ?? new CourierConnectionOptions();
        var clientName = $"courier-{name}";
        services.AddHttpClient(clientName, http => http.BaseAddress = options.BaseAddress);
        services.AddKeyedSingleton<ICourierGateway>(name.ToUpperInvariant(), (sp, _) =>
            createAdapter(sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName), options));
    }
}
