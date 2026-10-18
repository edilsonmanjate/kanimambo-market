using Kanimambo.Ordering.Application;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kanimambo.Ordering.Infrastructure;

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
        builder.Services.AddOrderingApplication();
        return builder;
    }
}
