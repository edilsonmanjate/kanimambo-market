using Kanimambo.Promotions.Application;
using Kanimambo.Promotions.Domain;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kanimambo.Promotions.Infrastructure;

public static class PromotionsInfrastructureExtensions
{
    public const string SeedDemoDataKey = "Promotions:SeedDemoData";

    public static IHostApplicationBuilder AddPromotionsModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var seedDemoData = builder.Configuration.GetValue(SeedDemoDataKey, false);
        builder.AddModuleDbContext<PromotionsDbContext>(PromotionsModule.Name, PromotionsDbContext.Schema, options =>
        {
            if (seedDemoData)
            {
                options.UseAsyncSeeding(PromotionsSeeder.SeedAsync);
            }
        });

        builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();
        builder.Services.AddPromotionsApplication();
        return builder;
    }
}

/// <summary>Demo promotions, written in the rule language exactly as marketing would type them.</summary>
public static class PromotionsSeeder
{
    public static readonly (string Name, string Rule, DiscountKind Kind, decimal Value)[] Demo =
    [
        ("M-Pesa em Maputo: 5% acima de 2 500 MZN", "cart.total >= 2500 AND customer.city = \"Maputo\" AND payment.method = \"MPesa\"", DiscountKind.Percentage, 5m),
        ("Cabazes: 150 MZN de desconto", "\"cabazes\" IN cart.categories", DiscountKind.FixedAmount, 150m),
        ("Clientes B2B da Matola e Maputo: 3%", "customer.type = \"b2b\" AND customer.city IN [\"Matola\", \"Maputo\"]", DiscountKind.Percentage, 3m),
    ];

    public static async Task SeedAsync(DbContext context, bool storeOperationPerformed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var db = (PromotionsDbContext)context;

        if (await db.Promotions.AnyAsync(cancellationToken))
        {
            return;
        }

        var createdAt = new DateTimeOffset(2025, 12, 31, 22, 0, 0, TimeSpan.Zero); // 2026-01-01 00:00 in Maputo, stored as UTC
        foreach (var (name, rule, kind, value) in Demo)
        {
            db.Promotions.Add(Promotion.Create(name, rule, PromotionDiscount.Create(kind, value).Value, null, null, "seed", createdAt).Value);
            createdAt = createdAt.AddMinutes(1);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
