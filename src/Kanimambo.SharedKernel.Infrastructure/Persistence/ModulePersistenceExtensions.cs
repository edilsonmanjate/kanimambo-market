using Kanimambo.SharedKernel.Infrastructure.Outbox;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

public static class ModulePersistenceExtensions
{
    public const string DatabaseConnectionName = "kanimambo";

    /// <summary>
    /// Registers a module's DbContext in its own schema (with its own migrations history table), plus the
    /// module's keyed <see cref="IUnitOfWork"/> and <see cref="IInboxStore"/> used by the Mediator behaviors.
    /// </summary>
    public static IHostApplicationBuilder AddModuleDbContext<TContext>(
        this IHostApplicationBuilder builder,
        string moduleName,
        string schema,
        Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext =>
        builder.AddModuleDbContext<TContext>(moduleName, schema, publishesEvents: false, configure);

    /// <summary>
    /// As above; with <paramref name="publishesEvents"/>, domain events are written to the module's outbox on
    /// SaveChanges (through the <see cref="IIntegrationEventMapper"/> registered with the key <c>typeof(TContext)</c>)
    /// and delivered by a background processor.
    /// </summary>
    public static IHostApplicationBuilder AddModuleDbContext<TContext>(
        this IHostApplicationBuilder builder,
        string moduleName,
        string schema,
        bool publishesEvents,
        Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration.GetConnectionString(DatabaseConnectionName);
        builder.Services.AddDbContextPool<TContext>((services, options) =>
        {
            UseModuleNpgsql(options, connectionString, schema);
            if (publishesEvents)
            {
                options.AddInterceptors(services.GetRequiredService<OutboxInterceptor<TContext>>());
            }

            configure?.Invoke(options);
        });
        builder.EnrichNpgsqlDbContext<TContext>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddKeyedScoped<IUnitOfWork>(moduleName, (sp, _) => new EfUnitOfWork<TContext>(sp.GetRequiredService<TContext>()));
        builder.Services.AddKeyedScoped<IInboxStore>(moduleName, (sp, _) =>
            new EfInboxStore<TContext>(sp.GetRequiredService<TContext>(), sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(new MigratableDbContext(typeof(TContext)));

        if (publishesEvents)
        {
            builder.Services.AddOptions<OutboxOptions>().Bind(builder.Configuration.GetSection(OutboxOptions.SectionName));
            // Each publishing module registers its mapper keyed by its DbContext type.
            builder.Services.AddSingleton(sp => new OutboxInterceptor<TContext>(
                sp.GetRequiredKeyedService<IIntegrationEventMapper>(typeof(TContext)), sp.GetRequiredService<TimeProvider>()));
            builder.Services.AddSingleton<OutboxProcessor<TContext>>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor<TContext>>());
        }

        return builder;
    }

    public static DbContextOptionsBuilder UseModuleNpgsql(DbContextOptionsBuilder options, string? connectionString, string schema) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema));
}
