using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Application.Regimes;
using Kanimambo.Invoicing.Application.Tax;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;
using Kanimambo.Invoicing.Infrastructure.Documents;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace Kanimambo.Invoicing.Infrastructure;

public static class InvoicingInfrastructureExtensions
{
    public static IHostApplicationBuilder AddInvoicingModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var section = builder.Configuration.GetSection(InvoicingOptions.SectionName);
        var options = section.Get<InvoicingOptions>() ?? new InvoicingOptions();
        var services = builder.Services;

        builder.AddModuleDbContext<InvoicingDbContext>(InvoicingModule.Name, InvoicingDbContext.Schema, publishesEvents: true, db =>
        {
            if (options.SeedDemoData)
            {
                db.UseAsyncSeeding(InvoicingSeeder.SeedAsync);
            }
        });
        services.AddKeyedSingleton<IIntegrationEventMapper, InvoicingIntegrationEventMapper>(typeof(InvoicingDbContext));
        services.AddOptions<InvoicingOptions>().Bind(section);

        // Singleton by DI lifetime: one cached, thread-safe rate table for the whole application (Catalog included).
        // It reloads itself when the configuration changes; nothing anywhere holds a static instance.
        services.AddSingleton(sp =>
        {
            var monitor = sp.GetRequiredService<IOptionsMonitor<InvoicingOptions>>();
            var table = new TaxRateTable(monitor.CurrentValue.Rates());
            monitor.OnChange(o => table.Load(o.Rates()));
            return table;
        });
        services.AddSingleton<ITaxRateTable>(sp => sp.GetRequiredService<TaxRateTable>());

        // Abstract Factory: one concrete factory per regime; the registry hands out whole kits.
        services.AddSingleton<IFiscalRegimeFactory>(sp => new IvaNormalFactory(sp.GetRequiredService<ITaxRateTable>(), options.IvaNormal));
        services.AddSingleton<IFiscalRegimeFactory>(_ => new IspcFactory(options.Ispc));
        services.AddSingleton<FiscalRegimeFactories>();

        var marketplace = options.Marketplace;
        services.AddSingleton(new InvoicingSettings(new FiscalParty(marketplace.Nuit, marketplace.LegalName, marketplace.Address), FiscalRegime.IvaNormal,
            marketplace.DeliveryTaxCategory, marketplace.DeliveryDescription, options.FallbackTaxCategory));

        // Object Pool: writers and buffers for bulk PDF and monthly-file generation.
        services.TryAddObjectPoolProvider();
        services.AddSingleton(sp => InvoicingPools.Create(sp.GetRequiredService<ObjectPoolProvider>()));
        services.AddSingleton<IMonthlyDeclarationExporter, MonthlyDeclarationExporter>();
        services.AddSingleton<IFiscalDocumentPdfRenderer, FiscalDocumentPdfRenderer>();

        services.AddScoped<IFiscalDocumentRepository, FiscalDocumentRepository>();
        services.AddScoped<ITaxpayerRepository, TaxpayerRepository>();
        services.AddScoped<IInvoiceRequestRepository, InvoiceRequestRepository>();
        services.AddScoped<IDocumentNumbering, DocumentNumbering>();
        return builder;
    }

    private static void TryAddObjectPoolProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(ObjectPoolProvider)))
        {
            services.AddSingleton<ObjectPoolProvider>(new DefaultObjectPoolProvider { MaximumRetained = Environment.ProcessorCount * 2 });
        }
    }
}
