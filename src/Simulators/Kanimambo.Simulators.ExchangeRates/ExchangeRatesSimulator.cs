using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Kanimambo.Simulators.ExchangeRates;

/// <summary>
/// Local stand-in for a daily exchange-rate API (USD/ZAR → MZN). It is deliberately slow and enforces a
/// call quota (HTTP 429), the two properties that justify the caching proxy in the Catalog module.
/// The rates are simulated, not official.
/// </summary>
public static class ExchangeRatesSimulator
{
    public const string RatePolicy = "rates";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        var options = builder.Configuration.GetSection(SimulatorOptions.SectionName).Get<SimulatorOptions>() ?? new SimulatorOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddFixedWindowLimiter(RatePolicy, window =>
            {
                window.PermitLimit = options.CallsPerMinute;
                window.Window = TimeSpan.FromMinutes(1);
                window.QueueLimit = 0;
                window.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapControllers();
        app.MapDefaultEndpoints();
        await app.RunAsync();
    }
}
