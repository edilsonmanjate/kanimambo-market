using Kanimambo.Api.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Api.Persistence;

internal static class PersistenceExtensions
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>Host-level tables (schema <c>platform</c>). Each module adds its own DbContext and schema.</summary>
    public static void AddPlatformPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<IdempotencyDbContext>(options =>
            IdempotencyDbContext.Configure(options, builder.Configuration.GetConnectionString(ConnectionNames.Database)));

        // Aspire adds health checks, connection retries and OpenTelemetry to the context.
        builder.EnrichNpgsqlDbContext<IdempotencyDbContext>();
    }

    /// <summary>Applies pending migrations; on by default in Development, opt-in elsewhere.</summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue(MigrateOnStartupKey, app.Environment.IsDevelopment()))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>().Database.MigrateAsync();
    }
}
