using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Application.Regimes;
using Kanimambo.Invoicing.Application.Tax;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;
using Kanimambo.Invoicing.Infrastructure.Documents;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace Kanimambo.Invoicing.Infrastructure;

public sealed class TaxRateOptions
{
    public string Category { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateOnly ValidFrom { get; set; }

    public string? ExemptionReason { get; set; }
}

public sealed class MarketplaceOptions
{
    public string Nuit { get; set; } = string.Empty;

    public string LegalName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string DeliveryTaxCategory { get; set; } = "standard";

    public string DeliveryDescription { get; set; } = "Serviço de entrega";
}

/// <summary>The <c>Invoicing</c> section: rates (with their legal history), per-regime series and mentions, the marketplace's own data.</summary>
public sealed class InvoicingOptions
{
    public const string SectionName = "Invoicing";

    public bool SeedDemoData { get; set; }

    public List<TaxRateOptions> TaxRates { get; } = [];

    public string FallbackTaxCategory { get; set; } = "standard";

    public RegimeOptions IvaNormal { get; set; } = new();

    public RegimeOptions Ispc { get; set; } = new() { InvoiceSeries = "FS", CreditNoteSeries = "NS" };

    public MarketplaceOptions Marketplace { get; set; } = new();

    public IEnumerable<TaxRate> Rates() => TaxRates.Select(r => new TaxRate(r.Category, r.Rate, r.ValidFrom, r.ExemptionReason));
}

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

/// <summary>Fiscal profiles for the Catalog's demo sellers (fictional NUITs), in both regimes.</summary>
public static class InvoicingSeeder
{
    public static readonly (Guid SellerId, string Nuit, string Name, string Address, FiscalRegime Regime)[] Demo =
    [
        (Guid.Parse("01920000-0000-7000-8000-000000000001"), "400100001", "Mercearia Xipamanine, Lda", "Mercado de Xipamanine, Maputo", FiscalRegime.Ispc),
        (Guid.Parse("01920000-0000-7000-8000-000000000002"), "400100002", "Grossista da Matola, SA", "Av. das Indústrias, Machava, Matola", FiscalRegime.IvaNormal),
        (Guid.Parse("01920000-0000-7000-8000-000000000003"), "400100003", "Armazém Chiveve, Lda", "Rua do Chiveve, Beira", FiscalRegime.IvaNormal),
        (Guid.Parse("01920000-0000-7000-8000-000000000004"), "400100004", "Cooperativa do Caju de Nampula", "Av. do Trabalho, Nampula", FiscalRegime.Ispc),
        (Guid.Parse("01920000-0000-7000-8000-000000000005"), "400100005", "Importadora Lebombo, Lda", "Av. Julius Nyerere, Maputo", FiscalRegime.IvaNormal),
        (Guid.Parse("01920000-0000-7000-8000-000000000006"), "400100006", "SolarTech Moçambique, Lda", "Av. Vladimir Lenine, Maputo", FiscalRegime.IvaNormal),
    ];

    public static async Task SeedAsync(DbContext context, bool storeOperationPerformed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var db = (InvoicingDbContext)context;
        if (await db.Taxpayers.AnyAsync(cancellationToken))
        {
            return;
        }

        var at = new DateTimeOffset(2025, 12, 31, 22, 0, 0, TimeSpan.Zero);
        foreach (var (sellerId, nuit, name, address, regime) in Demo)
        {
            db.Taxpayers.Add(TaxpayerProfile.Register(sellerId, nuit, name, address, regime, at).Value);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
