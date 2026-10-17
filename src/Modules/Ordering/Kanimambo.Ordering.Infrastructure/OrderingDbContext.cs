using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Infrastructure.Outbox;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kanimambo.Ordering.Infrastructure;

internal sealed class DraftSnapshotRecord
{
    public required Guid DraftId { get; init; }

    public required int Version { get; init; }

    public required string Payload { get; init; }
}

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public const string Schema = "ordering";
    public const string OrderNumberSequence = "OrderNumbers";

    private static readonly ValueConverter<OrderId, Guid> OrderIdConverter = new(id => id.Value, value => new OrderId(value));

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<DraftOrder> Drafts => Set<DraftOrder>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    internal DbSet<DraftSnapshotRecord> DraftSnapshots => Set<DraftSnapshotRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();
        modelBuilder.AddOutbox();
        modelBuilder.HasSequence<long>(OrderNumberSequence).StartsAt(1001);

        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("Orders");
            order.HasKey(o => o.Id);
            order.Property(o => o.Id).HasConversion(OrderIdConverter);
            order.HasIndex(o => o.Number).IsUnique();
            order.Property(o => o.CheckoutSource).HasMaxLength(16);
            order.HasIndex(o => new { o.CheckoutSource, o.CheckoutSourceId }).IsUnique();
            order.Property(o => o.DeliveryCity).HasMaxLength(100);
            order.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(32);
            order.Property(o => o.PaymentReference).HasMaxLength(100);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
            order.Property(o => o.PromotionName).HasMaxLength(200);
            order.Property<decimal>("_subtotal").HasColumnName("Subtotal").HasPrecision(18, 2);
            order.Property<decimal>("_discount").HasColumnName("Discount").HasPrecision(18, 2);
            order.Property<decimal>("_shippingFee").HasColumnName("ShippingFee").HasPrecision(18, 2);

            order.OwnsOne(o => o.Customer, customer =>
            {
                customer.Property(c => c.Id).HasColumnName("CustomerId").HasMaxLength(200);
                customer.Property(c => c.Type).HasColumnName("CustomerType").HasMaxLength(16);
                customer.Property(c => c.Phone).HasColumnName("CustomerPhone").HasMaxLength(20);
                customer.Property(c => c.Nuit).HasColumnName("CustomerNuit").HasMaxLength(9);
                customer.Ignore(c => c.IsBusiness);
                customer.HasIndex(c => c.Phone);
                customer.HasIndex(c => c.Id);
            });

            order.OwnsMany(o => o.Lines, line => MapLine(line, "OrderLines"));
            order.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);

            order.OwnsMany(o => o.History, change =>
            {
                change.ToTable("OrderStatusChanges");
                change.WithOwner().HasForeignKey("OrderId");
                change.Property<int>("Id");
                change.HasKey("Id");
                change.Property(c => c.From).HasConversion<string>().HasMaxLength(32);
                change.Property(c => c.To).HasConversion<string>().HasMaxLength(32);
                change.Property(c => c.Reason).HasMaxLength(500);
            });
            order.Navigation(o => o.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);

            order.Ignore(o => o.Totals);
            order.Ignore(o => o.State);
            order.Ignore(o => o.AllowedActions);
            order.Ignore(o => o.PaysOnDelivery);
            order.Ignore(o => o.DisplayNumber);
            order.Ignore(o => o.DomainEvents);
        });

        modelBuilder.Entity<DraftOrder>(draft =>
        {
            draft.ToTable("DraftOrders");
            draft.HasKey(d => d.Id);
            draft.Property(d => d.CustomerId).HasMaxLength(200);
            draft.Property(d => d.DeliveryCity).HasMaxLength(100);
            draft.Property(d => d.PaymentMethod).HasConversion<string>().HasMaxLength(32);
            draft.OwnsMany(d => d.Lines, line => MapLine(line, "DraftOrderLines"));
            draft.Navigation(d => d.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
            draft.Ignore(d => d.IsSubmitted);
            draft.Ignore(d => d.DomainEvents);
        });

        modelBuilder.Entity<DraftSnapshotRecord>(snapshot =>
        {
            snapshot.ToTable("DraftOrderSnapshots");
            snapshot.HasKey(s => new { s.DraftId, s.Version });
            snapshot.Property(s => s.Payload).HasColumnType("jsonb");
        });
    }

    private static void MapLine<TOwner>(OwnedNavigationBuilder<TOwner, OrderLine> line, string table)
        where TOwner : class
    {
        line.ToTable(table);
        line.Property<int>("Id");
        line.HasKey("Id");
        line.Property(l => l.Sku).HasMaxLength(64);
        line.Property(l => l.Name).HasMaxLength(300);
        line.Property<decimal>("_unitPriceMzn").HasColumnName("UnitPriceMzn").HasPrecision(18, 2);
        line.Property<decimal>("_lineTotalMzn").HasColumnName("LineTotalMzn").HasPrecision(18, 2);
        line.Ignore(l => l.UnitPrice);
        line.Ignore(l => l.LineTotal);
    }
}

internal sealed class OrderingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderingDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", OrderingDbContext.Schema);
        return new OrderingDbContext(options.Options);
    }
}
