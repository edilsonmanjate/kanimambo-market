using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Kanimambo.Simulators.Courier;
using Kanimambo.Simulators.ExchangeRates;
using Kanimambo.Simulators.Messaging;
using Kanimambo.Simulators.MobileMoney;
using Kanimambo.Simulators.Wholesaler;
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
/// data applied), with the external-system simulators running in-process at zero latency behind the
/// API's real HTTP clients. Derive once per test assembly and register it as an assembly fixture.
/// </summary>
public abstract class KanimamboWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ExchangeRatesHost = "exchange-rates.test";
    private const string WholesalerHost = "wholesaler.test";
    private const string MobileMoneyHost = "mobile-money.test";
    private const string CourierHost = "courier.test";
    private const string MessagingHost = "messaging.test";
    private const string ApiHost = "api.test";

    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("kanimambo-integration-tests-signing-key-not-a-secret"));

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private WebApplicationFactory<Ledger>? _mobileMoney;
    private WebApplicationFactory<Waybills>? _courier;

    public WebApplicationFactory<RatesController> ExchangeRates { get; } = Simulator<RatesController>("Simulator:LatencyMilliseconds");

    public WebApplicationFactory<ProductsController> Wholesaler { get; } = Simulator<ProductsController>("Simulator:LatencyMillisecondsPerPage");

    /// <summary>SMS, WhatsApp and e-mail gateways; read what customers received with <c>GET /inbox?to=</c>.</summary>
    public WebApplicationFactory<Inbox> Messaging { get; } = new WebApplicationFactory<Inbox>();

    /// <summary>Mobile-money operators; their callbacks are routed back into this API.</summary>
    public WebApplicationFactory<Ledger> MobileMoney => _mobileMoney ??= new WebApplicationFactory<Ledger>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Simulator:ConfirmationDelayMilliseconds", ((int)CustomerConfirmationDelay.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Simulator:DelayedCallbackMilliseconds", ((int)DelayedCallback.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(services => services.AddHttpClient(MobileMoneySimulator.CallbackClient)
            .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler()));
    });

    /// <summary>How long the simulated customer takes to type the PIN. Long by default so tests drive payments explicitly.</summary>
    protected virtual TimeSpan CustomerConfirmationDelay => TimeSpan.FromMinutes(5);

    protected virtual TimeSpan DelayedCallback => TimeSpan.FromMinutes(10);

    protected virtual TimeSpan PinTimeout => TimeSpan.FromMinutes(5);

    /// <summary>Couriers (Txova, Mbila); their tracking webhooks are routed back into this API.</summary>
    public WebApplicationFactory<Waybills> Courier => _courier ??= new WebApplicationFactory<Waybills>().WithWebHostBuilder(builder =>
    {
        var step = ((int)CourierStepDelay.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        builder.UseSetting("Simulator:PickupDelayMilliseconds", step);
        builder.UseSetting("Simulator:TransitDelayMilliseconds", step);
        builder.UseSetting("Simulator:DeliveryDelayMilliseconds", step);
        builder.ConfigureTestServices(services => services.AddHttpClient(CourierSimulator.WebhookClient)
            .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler()));
    });

    /// <summary>Time between courier tracking events. Long by default so tests drive deliveries explicitly.</summary>
    protected virtual TimeSpan CourierStepDelay => TimeSpan.FromMinutes(5);

    public string DatabaseConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

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

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:kanimambo", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", _redis.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Catalog:SeedDemoData", "true");
        builder.UseSetting("Promotions:SeedDemoData", "true");
        builder.UseSetting("Catalog:ExchangeRates:BaseAddress", $"http://{ExchangeRatesHost}/");
        builder.UseSetting("Catalog:Wholesaler:BaseAddress", $"http://{WholesalerHost}/");
        builder.UseSetting("Payments:CallbackBaseAddress", $"http://{ApiHost}/");
        builder.UseSetting("Payments:PinTimeout", PinTimeout.ToString());
        builder.UseSetting("Payments:TimeoutCheckInterval", "00:00:00.200");
        builder.UseSetting("Payments:GatewayRetryBaseDelay", "00:00:00.050");
        builder.UseSetting("Shipping:WebhookBaseAddress", $"http://{ApiHost}/");
        builder.UseSetting("Notifications:Gateway:BaseAddress", $"http://{MessagingHost}/");
        foreach (var courier in new[] { "Txova", "Mbila" })
        {
            builder.UseSetting($"Shipping:Couriers:{courier}:BaseAddress", $"http://{CourierHost}/");
        }

        foreach (var op in new[] { "MPesa", "EMola", "MKesh" })
        {
            builder.UseSetting($"Payments:Operators:{op}:BaseAddress", $"http://{MobileMoneyHost}/");
        }

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

            services.ConfigureHttpClientDefaults(http => http.ConfigurePrimaryHttpMessageHandler(() => new SimulatorRouter(
                new Dictionary<string, HttpMessageInvoker>(StringComparer.OrdinalIgnoreCase)
                {
                    [ExchangeRatesHost] = new(ExchangeRates.Server.CreateHandler()),
                    [WholesalerHost] = new(Wholesaler.Server.CreateHandler()),
                    [MobileMoneyHost] = new(MobileMoney.Server.CreateHandler()),
                    [CourierHost] = new(Courier.Server.CreateHandler()),
                    [MessagingHost] = new(Messaging.Server.CreateHandler()),
                })));

            ConfigureTestServices(services);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await ExchangeRates.DisposeAsync();
        await Wholesaler.DisposeAsync();
        await Messaging.DisposeAsync();
        if (_mobileMoney is not null)
        {
            await _mobileMoney.DisposeAsync();
        }

        if (_courier is not null)
        {
            await _courier.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static WebApplicationFactory<T> Simulator<T>(string latencySetting)
        where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(builder => builder.UseSetting(latencySetting, "0"));
}
