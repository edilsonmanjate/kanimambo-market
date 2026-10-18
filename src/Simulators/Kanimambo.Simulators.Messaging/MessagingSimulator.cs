namespace Kanimambo.Simulators.Messaging;

/// <summary>
/// Local stand-in for an SMS gateway, a WhatsApp business API and an e-mail service (simulated formats, not any
/// provider's real API). It enforces what real gateways enforce — an SMS is GSM-7 text of at most 160 characters —
/// and keeps every accepted message in an inbox that can be read back (<c>GET /inbox?to=</c>).
/// Recipient phones ending in 0201 hit an SMS gateway outage (503); ending in 0202 are not on WhatsApp.
/// </summary>
public static class MessagingSimulator
{
    public const string ApiKeyHeader = "X-Api-Key";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();
        builder.Services.AddSingleton<Inbox>();
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.MapControllers();
        app.MapDefaultEndpoints();
        await app.RunAsync();
    }
}
