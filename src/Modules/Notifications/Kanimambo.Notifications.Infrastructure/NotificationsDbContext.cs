using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
