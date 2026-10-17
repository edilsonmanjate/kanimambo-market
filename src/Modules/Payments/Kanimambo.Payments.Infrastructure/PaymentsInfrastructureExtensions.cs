using Kanimambo.Payments.Application;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Application.Processing;
using Kanimambo.Payments.Domain;
using Kanimambo.Payments.Infrastructure.Gateways.Adapters;
using Kanimambo.Payments.Infrastructure.Gateways.Decorators;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Payments.Infrastructure;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>Public address of this API, used to build the callback URLs given to operators.</summary>
    public Uri CallbackBaseAddress { get; set; } = new("http://localhost:5080/");

    public TimeSpan PinTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan TimeoutCheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    public int GatewayMaxRetries { get; set; } = 2;

    public TimeSpan GatewayRetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Per-call timeout towards an operator; a timeout is a transient error and is retried.</summary>
    public TimeSpan GatewayTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public Dictionary<MobileOperator, OperatorOptions> Operators { get; } = [];
}

public static class PaymentsInfrastructureExtensions
{
    public static IHostApplicationBuilder AddPaymentsModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Configuration.GetSection(PaymentsOptions.SectionName).Get<PaymentsOptions>() ?? new PaymentsOptions();
        var services = builder.Services;

        // Payments publishes its outcomes through its own outbox.
        builder.AddModuleDbContext<PaymentsDbContext>(PaymentsModule.Name, PaymentsDbContext.Schema, publishesEvents: true);
        services.AddKeyedSingleton<IIntegrationEventMapper, PaymentsIntegrationEventMapper>(typeof(PaymentsDbContext));

        services.AddSingleton(options);
        services.AddSingleton(new PaymentSettings(options.PinTimeout, options.CallbackBaseAddress));
        services.AddSingleton(new OperatorDirectory(options.Operators.ToDictionary(o => o.Key, o => (IReadOnlyCollection<string>)o.Value.Prefixes)));
        services.AddSingleton<GatewayMetrics>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<PaymentProcessorFactory>();
        services.AddSingleton<ICardAcquirer, SimulatedCardAcquirer>();
        services.AddSingleton<IMobileMoneyGatewayProvider, KeyedGatewayProvider>();
        services.AddHostedService<PaymentTimeoutMonitor>();

        AddGateway(services, MobileOperator.MPesa, options, (http, o) => new MPesaAdapter(http, o));
        AddGateway(services, MobileOperator.EMola, options, (http, o) => new EMolaAdapter(http, o));
        AddGateway(services, MobileOperator.MKesh, options, (http, o) => new MKeshAdapter(http, o));
        return builder;
    }

    /// <summary>
    /// Decorator chain, outermost first — the order is visible right here:
    /// <c>Logging → Metrics → Caching (final statuses) → Retry (transient errors) → operator Adapter</c>.
    /// </summary>
    private static void AddGateway(
        IServiceCollection services,
        MobileOperator mobileOperator,
        PaymentsOptions options,
        Func<HttpClient, OperatorOptions, IMobileMoneyGateway> createAdapter)
    {
        var operatorOptions = options.Operators.GetValueOrDefault(mobileOperator) ?? new OperatorOptions();
        var clientName = $"mobile-money-{mobileOperator}";
        // No generic HTTP resilience here: it would retry payment POSTs blindly underneath the business-aware
        // RetryingGatewayDecorator (which knows which errors are transient and how to recover a duplicate).
        // RemoveAllResilienceHandlers is the documented way to opt one client out of ConfigureHttpClientDefaults;
        // it is still flagged experimental (EXTEXP0001), hence the scoped suppression.
#pragma warning disable EXTEXP0001
        services.AddHttpClient(clientName, http =>
            {
                http.BaseAddress = operatorOptions.BaseAddress;
                http.Timeout = options.GatewayTimeout;
            })
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        services.AddKeyedSingleton<IMobileMoneyGateway>(mobileOperator, (sp, _) =>
        {
            IMobileMoneyGateway gateway = createAdapter(sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName), operatorOptions);
            gateway = new RetryingGatewayDecorator(gateway, options.GatewayMaxRetries, options.GatewayRetryBaseDelay);
            gateway = new CachingGatewayDecorator(gateway, sp.GetRequiredService<IDistributedCache>());
            gateway = new MetricsGatewayDecorator(gateway, sp.GetRequiredService<GatewayMetrics>());
            gateway = new LoggingGatewayDecorator(gateway, sp.GetRequiredService<ILogger<LoggingGatewayDecorator>>());
            return gateway;
        });
    }
}
