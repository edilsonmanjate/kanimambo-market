using Kanimambo.Notifications.Application;
using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.Notifications.Infrastructure.Channels;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kanimambo.Notifications.Infrastructure;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<ContactPreferences> Preferences => Set<ContactPreferences>();

    public DbSet<SentNotification> Sent => Set<SentNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();

        modelBuilder.Entity<ContactPreferences>(preferences =>
        {
            preferences.ToTable("ContactPreferences");
            preferences.HasKey(p => p.Id);
            preferences.Property(p => p.Id).HasColumnName("Phone").HasMaxLength(12).ValueGeneratedNever();
            preferences.Property(p => p.OwnerId).HasMaxLength(200);
            preferences.Property(p => p.Email).HasMaxLength(254);
            preferences.Property(p => p.Channels)
                .HasMaxLength(64)
                .HasConversion(
                    channels => string.Join(',', channels),
                    text => text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<ChannelKind>).ToList(),
                    new ValueComparer<IReadOnlyList<ChannelKind>>(
                        (a, b) => a != null && b != null && a.SequenceEqual(b),
                        channels => channels.Aggregate(0, (hash, c) => HashCode.Combine(hash, c)),
                        channels => channels.ToList()));
            preferences.Ignore(p => p.Phone);
            preferences.Ignore(p => p.DomainEvents);
        });

        modelBuilder.Entity<SentNotification>(sent =>
        {
            sent.ToTable("SentNotifications");
            sent.HasKey(n => n.Id);
            sent.HasIndex(n => n.Recipient);
            sent.HasIndex(n => n.OrderNumber);
            sent.HasIndex(n => n.EventId);
            sent.Property(n => n.MessageType).HasMaxLength(64);
            sent.Property(n => n.Channel).HasConversion<string>().HasMaxLength(16);
            sent.Property(n => n.Status).HasConversion<string>().HasMaxLength(16);
            sent.Property(n => n.Recipient).HasMaxLength(254);
            sent.Property(n => n.ProviderMessageId).HasMaxLength(100);
            sent.Property(n => n.Error).HasMaxLength(500);
            sent.Ignore(n => n.DomainEvents);
        });
    }
}

internal sealed class NotificationsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", NotificationsDbContext.Schema);
        return new NotificationsDbContext(options.Options);
    }
}

internal sealed class ContactPreferencesRepository(NotificationsDbContext db) : IContactPreferencesRepository
{
    public void Add(ContactPreferences preferences) => db.Preferences.Add(preferences);

    public Task<ContactPreferences?> GetAsync(string phone, CancellationToken cancellationToken) =>
        db.Preferences.SingleOrDefaultAsync(p => p.Id == phone, cancellationToken);
}

internal sealed class SentNotificationRepository(NotificationsDbContext db) : ISentNotificationRepository
{
    public void Add(SentNotification notification) => db.Sent.Add(notification);

    public async Task<IReadOnlyList<SentNotification>> ListAsync(string? recipient, long? orderNumber, CancellationToken cancellationToken) =>
        await db.Sent.AsNoTracking()
            .Where(n => (recipient == null || n.Recipient == recipient) && (orderNumber == null || n.OrderNumber == orderNumber))
            .OrderBy(n => n.At)
            .Take(500)
            .ToListAsync(cancellationToken);
}

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Base of the links in messages (order tracking, invoice download).</summary>
    public Uri PublicBaseAddress { get; set; } = new("https://kanimambo.co.mz/");

    /// <summary>Channels for customers without preferences: SMS reaches every phone.</summary>
    public List<ChannelKind> DefaultChannels { get; } = [];

    public GatewayOptions Gateway { get; set; } = new();
}

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
