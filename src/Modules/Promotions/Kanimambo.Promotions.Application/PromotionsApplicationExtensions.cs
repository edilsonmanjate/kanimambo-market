using Kanimambo.Promotions.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Promotions.Application;

public static class PromotionsApplicationExtensions
{
    public static IServiceCollection AddPromotionsApplication(this IServiceCollection services)
    {
        services.AddScoped<IPromotionEvaluator, PromotionEvaluator>();
        return services;
    }
}
