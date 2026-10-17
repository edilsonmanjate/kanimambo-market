using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kanimambo.Api.Idempotency;

internal static class IdempotencyExtensions
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
            .Validate(o => o.RecordTimeToLive > TimeSpan.Zero && o.InFlightTimeout > TimeSpan.Zero && o.CleanupInterval > TimeSpan.Zero,
                "Idempotency durations must be positive.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IdempotencyStore>();
        services.AddHostedService<IdempotencyCleanupService>();
        return services;
    }

    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app) =>
        app.UseMiddleware<IdempotencyMiddleware>();
}
