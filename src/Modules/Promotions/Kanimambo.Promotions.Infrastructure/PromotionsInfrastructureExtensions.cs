using Kanimambo.Promotions.Application;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
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
