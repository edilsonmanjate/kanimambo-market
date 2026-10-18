namespace Kanimambo.Simulators.Wholesaler;

/// <summary>
/// Local stand-in for a wholesaler's catalog API: paginated and slow (configurable latency per page),
/// with a deterministic catalog of a few thousand SKUs. Exercises the Iterator in the Catalog module.
/// </summary>
public static class WholesalerSimulator
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        var options = builder.Configuration.GetSection(WholesalerSimulatorOptions.SectionName).Get<WholesalerSimulatorOptions>()
            ?? new WholesalerSimulatorOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new WholesalerCatalogData(options.ProductCount));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.MapControllers();
        app.MapDefaultEndpoints();
        await app.RunAsync();
    }
}
