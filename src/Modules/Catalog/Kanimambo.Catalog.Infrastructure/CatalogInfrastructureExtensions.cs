using System.Threading.RateLimiting;
using Kanimambo.Catalog.Application;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Backoffice;
using Kanimambo.Catalog.Application.Imports;
using Kanimambo.Catalog.Contracts;
using Kanimambo.Catalog.Infrastructure.Contracts;
using Kanimambo.Catalog.Infrastructure.ExchangeRates;
using Kanimambo.Catalog.Infrastructure.Imports;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.Catalog.Infrastructure.Tax;
using Kanimambo.Catalog.Infrastructure.Wholesaler;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kanimambo.Catalog.Infrastructure;

public static class CatalogInfrastructureExtensions
{
    public const string SeedDemoDataKey = "Catalog:SeedDemoData";

    public static IHostApplicationBuilder AddCatalogModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var seedDemoData = builder.Configuration.GetValue(SeedDemoDataKey, false);
        builder.AddModuleDbContext<CatalogDbContext>(CatalogModule.Name, CatalogDbContext.Schema, options =>
        {
            if (seedDemoData)
            {
                options.UseAsyncSeeding(CatalogSeeder.SeedAsync);
            }
        });

        var services = builder.Services;

        services.AddScoped<ISellerRepository, SellerRepository>();
        services.AddScoped<ICatalogItemRepository, CatalogItemRepository>();
        services.AddScoped<ICatalogSearch, CatalogSearch>();
        services.AddScoped<IBackofficeOperationLog, BackofficeOperationLog>();
        services.AddScoped<BackofficeContext>();
        services.AddScoped<BackofficeInvoker>();

        // Public contracts consumed by other modules (Cart, Ordering, ...).
        services.AddScoped<ICatalogReader, CatalogReader>();
        services.AddScoped<ICurrencyConverter, CurrencyConverter>();

        // IVA rates come from the Invoicing module's cached rate table (a DI singleton), through its contract.
        services.AddSingleton<ITaxRateTable, InvoicingTaxRates>();

        // Template Method: one importer per format, all sharing the same fixed pipeline.
        services.AddScoped<ICatalogBatchWriter, CatalogBatchWriter>();
        services.AddScoped<ICatalogImporter, CsvCatalogImporter>();
        services.AddScoped<ICatalogImporter, ExcelCatalogImporter>();
        services.AddScoped<ICatalogImporter, XmlCatalogImporter>();
        services.AddScoped<ICatalogImporter, WholesalerApiImporter>();

        // Iterator over the wholesaler API (standard resilience comes from ServiceDefaults).
        services.AddOptions<WholesalerOptions>().Bind(builder.Configuration.GetSection(WholesalerOptions.SectionName));
        services.AddHttpClient<IWholesalerCatalog, WholesalerCatalogClient>((sp, http) =>
            http.BaseAddress = sp.GetRequiredService<IOptions<WholesalerOptions>>().Value.BaseAddress);

        // Proxy: the application only ever sees the caching, rate-limited proxy around the HTTP subject.
        services.AddOptions<ExchangeRatesOptions>().Bind(builder.Configuration.GetSection(ExchangeRatesOptions.SectionName));
        services.AddHttpClient(HttpExchangeRateClient.HttpClientName, (sp, http) =>
            http.BaseAddress = sp.GetRequiredService<IOptions<ExchangeRatesOptions>>().Value.BaseAddress);
        services.AddSingleton<HttpExchangeRateClient>();
        services.AddSingleton<IExchangeRateProvider>(sp =>
        {
            var quota = sp.GetRequiredService<IOptions<ExchangeRatesOptions>>().Value.MaxUpstreamCallsPerMinute;
            var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = quota,
                TokensPerPeriod = quota,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });

            return new CachingExchangeRateProxy(
                sp.GetRequiredService<HttpExchangeRateClient>(),
                sp.GetRequiredService<IDistributedCache>(),
                limiter,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<CachingExchangeRateProxy>>());
        });

        return builder;
    }
}
