using Kanimambo.Notifications.Application;
using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.Notifications.Infrastructure.Channels;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kanimambo.Notifications.Infrastructure;

public static class NotificationsInfrastructureExtensions
{
    public static IHostApplicationBuilder AddNotificationsModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = builder.Configuration.GetSection(NotificationsOptions.SectionName).Get<NotificationsOptions>() ?? new NotificationsOptions();
        var services = builder.Services;

        builder.AddModuleDbContext<NotificationsDbContext>(NotificationsModule.Name, NotificationsDbContext.Schema);
        services.AddSingleton(new NotificationSettings(options.PublicBaseAddress, options.DefaultChannels.Count > 0 ? [.. options.DefaultChannels] : [ChannelKind.Sms]));
        services.AddScoped<IContactPreferencesRepository, ContactPreferencesRepository>();
        services.AddScoped<ISentNotificationRepository, SentNotificationRepository>();

        // Bridge implementors: one per channel, each with its own gateway client. A new channel is one class and one line.
        // No generic HTTP retries: re-posting a message can deliver it twice, and a channel that fails should fall back
        // to the customer's next channel at once; the outbox retries later when every channel is down.
        services.AddSingleton(options.Gateway);
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers: the documented opt-out from ConfigureHttpClientDefaults
        services.AddHttpClient<SmsChannel>(http => Configure(http, options.Gateway)).RemoveAllResilienceHandlers();
        services.AddHttpClient<WhatsAppChannel>(http => Configure(http, options.Gateway)).RemoveAllResilienceHandlers();
        services.AddHttpClient<EmailChannel>(http => Configure(http, options.Gateway)).RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        services.AddTransient<INotificationChannel>(sp => sp.GetRequiredService<SmsChannel>());
        services.AddTransient<INotificationChannel>(sp => sp.GetRequiredService<WhatsAppChannel>());
        services.AddTransient<INotificationChannel>(sp => sp.GetRequiredService<EmailChannel>());
        services.AddScoped<NotificationChannels>();
        return builder;
    }

    private static void Configure(HttpClient http, GatewayOptions gateway)
    {
        http.BaseAddress = gateway.BaseAddress;
        http.Timeout = gateway.Timeout;
    }
}
