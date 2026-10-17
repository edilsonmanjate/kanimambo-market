using Kanimambo.Api.IntegrationTests;
using Kanimambo.Api.IntegrationTests.Probes;
using Kanimambo.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(KanimamboApiFactory))]

namespace Kanimambo.Api.IntegrationTests;

/// <summary>
/// Boots the real API host against throwaway PostgreSQL and Redis containers, once per test assembly.
/// Test-only probe controllers exercise the cross-cutting HTTP behavior before any module exists.
/// </summary>
public sealed class KanimamboApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // Start the host eagerly: lazy creation is not thread-safe, and test classes running in
        // parallel would otherwise race to build separate hosts with separate singletons.
        _ = Server;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting($"ConnectionStrings:{ConnectionNames.Database}", _postgres.GetConnectionString());
        builder.UseSetting($"ConnectionStrings:{ConnectionNames.Redis}", _redis.GetConnectionString());
        builder.UseSetting(PersistenceExtensions.MigrateOnStartupKey, "true");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ProbeState>();
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
