using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

/// <summary>Marks a DbContext whose migrations the host applies at startup.</summary>
public sealed record MigratableDbContext(Type ContextType);

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
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration.GetConnectionString(DatabaseConnectionName);
        builder.Services.AddDbContextPool<TContext>(options =>
        {
            UseModuleNpgsql(options, connectionString, schema);
            configure?.Invoke(options);
        });
        builder.EnrichNpgsqlDbContext<TContext>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddKeyedScoped<IUnitOfWork>(moduleName, (sp, _) => new EfUnitOfWork<TContext>(sp.GetRequiredService<TContext>()));
        builder.Services.AddKeyedScoped<IInboxStore>(moduleName, (sp, _) =>
            new EfInboxStore<TContext>(sp.GetRequiredService<TContext>(), sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(new MigratableDbContext(typeof(TContext)));

        return builder;
    }

    public static DbContextOptionsBuilder UseModuleNpgsql(DbContextOptionsBuilder options, string? connectionString, string schema) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema));
}
