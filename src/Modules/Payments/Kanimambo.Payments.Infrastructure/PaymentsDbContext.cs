using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Infrastructure.Outbox;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Payments.Infrastructure;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();
        modelBuilder.AddOutbox();

        modelBuilder.Entity<Payment>(payment =>
        {
            payment.ToTable("Payments");
            payment.HasKey(p => p.Id);
            payment.HasIndex(p => new { p.OrderId, p.Attempt }).IsUnique();
            payment.HasIndex(p => new { p.Status, p.PinExpiresAt });
            payment.Property(p => p.Method).HasConversion<string>().HasMaxLength(32);
            payment.Property(p => p.Operator).HasConversion<string>().HasMaxLength(16);
            payment.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
            payment.Property(p => p.FailureReason).HasConversion<string>().HasMaxLength(32);
            payment.Property(p => p.Phone).HasMaxLength(20);
            payment.Property(p => p.OperatorTransactionId).HasMaxLength(100);
            payment.Property(p => p.NativeCode).HasMaxLength(32);
            payment.Property<decimal>("_amountMzn").HasColumnName("AmountMzn").HasPrecision(18, 2);
            payment.OwnsMany(p => p.History, history =>
            {
                history.ToTable("PaymentTransitions");
                history.WithOwner().HasForeignKey("PaymentId");
                history.Property<int>("Id");
                history.HasKey("Id");
                history.Property(h => h.From).HasConversion<string>().HasMaxLength(32);
                history.Property(h => h.To).HasConversion<string>().HasMaxLength(32);
                history.Property(h => h.Detail).HasMaxLength(500);
            });
            payment.Navigation(p => p.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
            payment.Ignore(p => p.Amount);
            payment.Ignore(p => p.State);
            payment.Ignore(p => p.DomainEvents);
        });
    }
}
