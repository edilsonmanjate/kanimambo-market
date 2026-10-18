using Kanimambo.SharedKernel.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.SharedKernel.Infrastructure.Features;

public static class FeatureFlagExtensions
{
    /// <summary>One instance for the application lifetime: the DI container, not a static field, owns it.</summary>
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FeatureFlagOptions>().Bind(configuration.GetSection(FeatureFlagOptions.SectionName));
        services.AddSingleton<IFeatureFlags, ConfiguredFeatureFlags>();
        return services;
    }
}
