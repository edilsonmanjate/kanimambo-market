using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Kanimambo.Testing;

/// <summary>
/// Boots the real API host against throwaway PostgreSQL and Redis containers (migrations and demo seed
/// data applied). Derive once per test assembly and register it as an assembly fixture.
/// </summary>
public abstract class KanimamboWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("kanimambo-integration-tests-signing-key-not-a-secret"));

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public string DatabaseConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        await OnContainersStartedAsync();

        // Start the host eagerly: lazy creation is not thread-safe, and test classes running in
        // parallel would otherwise race to build separate hosts with separate singletons.
        _ = Server;
    }

    /// <summary>Creates a client whose requests carry a bearer token for the given user and roles.</summary>
    public HttpClient CreateClientAs(string userName, params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userName, roles));
        return client;
    }

    public static string CreateToken(string userName, params string[] roles)
    {
        var claims = new List<Claim> { new("unique_name", userName), new("sub", userName) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        });
    }

    protected virtual Task OnContainersStartedAsync() => Task.CompletedTask;

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:kanimambo", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", _redis.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Catalog:SeedDemoData", "true");

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = SigningKey,
                });

            ConfigureTestServices(services);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
