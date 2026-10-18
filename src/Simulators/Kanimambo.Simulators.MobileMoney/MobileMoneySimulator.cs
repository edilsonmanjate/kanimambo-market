namespace Kanimambo.Simulators.MobileMoney;

/// <summary>
/// Local stand-in for three mobile-money operators (M-Pesa, e-Mola, mKesh). Each speaks its own simulated
/// format — these are NOT the official APIs. Payments are asynchronous: the request is accepted, the
/// "customer confirms the PIN" after a delay, and the result is POSTed (HMAC-signed) to the callback URL.
/// The last four digits of the phone number choose the scenario (see <see cref="Scenario"/>).
/// </summary>
public static class MobileMoneySimulator
{
    public const string CallbackClient = "callbacks";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        var options = builder.Configuration.GetSection(SimulatorOptions.SectionName).Get<SimulatorOptions>() ?? new SimulatorOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<Ledger>();
        builder.Services.AddSingleton<CallbackDispatcher>();
        builder.Services.AddHttpClient(CallbackClient);
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.MapControllers();
        app.MapDefaultEndpoints();
        await app.RunAsync();
    }
}
