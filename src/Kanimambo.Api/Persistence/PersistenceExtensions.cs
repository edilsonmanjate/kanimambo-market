using Kanimambo.Api.Idempotency;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Api.Persistence;

internal static class PersistenceExtensions
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>Host-level tables (schema <c>platform</c>). Each module registers its own DbContext and schema.</summary>
    public static void AddPlatformPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<IdempotencyDbContext>(options =>
            IdempotencyDbContext.Configure(options, builder.Configuration.GetConnectionString(ConnectionNames.Database)));

        // Aspire adds health checks, connection retries and OpenTelemetry to the context.
        builder.EnrichNpgsqlDbContext<IdempotencyDbContext>();
        builder.Services.AddSingleton(new MigratableDbContext(typeof(IdempotencyDbContext)));
    }

    /// <summary>Applies pending migrations of the host and every module; on by default in Development, opt-in elsewhere.</summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue(MigrateOnStartupKey, app.Environment.IsDevelopment()))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        foreach (var migratable in scope.ServiceProvider.GetServices<MigratableDbContext>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(migratable.ContextType);
            await context.Database.MigrateAsync();
        }
    }
}
