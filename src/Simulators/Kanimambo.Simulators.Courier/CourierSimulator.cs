namespace Kanimambo.Simulators.Courier;

/// <summary>
/// Local stand-in for two <em>fictional</em> couriers: "Txova Express" (same-day riders in Maputo/Matola) and
/// "Mbila Cargo" (inter-provincial cargo). Each speaks its own simulated format. A booking is accepted, then the
/// courier reports progress (picked up, in transit, delivered) through HMAC-signed webhooks, retried until the
/// merchant acknowledges them. The last four digits of the recipient's phone choose the scenario (see <see cref="Scenario"/>).
/// </summary>
public static class CourierSimulator
{
    public const string WebhookClient = "webhooks";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        var options = builder.Configuration.GetSection(SimulatorOptions.SectionName).Get<SimulatorOptions>() ?? new SimulatorOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<Waybills>();
        builder.Services.AddSingleton<WebhookDispatcher>();
        builder.Services.AddHttpClient(WebhookClient);
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.MapControllers();
        app.MapDefaultEndpoints();
        await app.RunAsync();
    }
}
