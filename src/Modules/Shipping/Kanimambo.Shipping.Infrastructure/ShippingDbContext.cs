using Kanimambo.SharedKernel.Infrastructure.Outbox;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.Shipping.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Shipping.Infrastructure;

public sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options) : DbContext(options)
{
    public const string Schema = "shipping";

    public DbSet<Consignment> Consignments => Set<Consignment>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();
        modelBuilder.AddOutbox();

        modelBuilder.Entity<Consignment>(consignment =>
        {
            consignment.ToTable("Consignments");
            consignment.HasKey(c => c.Id);
            consignment.HasIndex(c => c.OrderId).IsUnique();
            consignment.Property(c => c.Method).HasMaxLength(32);
            consignment.Property(c => c.DestinationCity).HasMaxLength(100);
            consignment.Property(c => c.District).HasMaxLength(100);
            consignment.Property(c => c.PickupPointId).HasMaxLength(64);
            consignment.Property(c => c.RecipientPhone).HasMaxLength(20);
            consignment.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);

            // Every parcel update also updates the consignment row, so xmin serialises concurrent webhooks.
            consignment.Property<uint>("RowVersion").IsRowVersion();

            consignment.HasMany(c => c.Parcels).WithOne().HasForeignKey("ConsignmentId").OnDelete(DeleteBehavior.Cascade);
            consignment.Navigation(c => c.Parcels).HasField("_parcels").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
            consignment.Ignore(c => c.IsPickup);
            consignment.Ignore(c => c.DomainEvents);
        });

        modelBuilder.Entity<Parcel>(parcel =>
        {
            parcel.ToTable("Parcels");
            parcel.HasKey(p => p.Id);
            parcel.Property(p => p.Id).ValueGeneratedNever();
            parcel.HasIndex(p => p.Reference).IsUnique();
            parcel.Property(p => p.Reference).HasMaxLength(40);
            parcel.Property(p => p.OriginCity).HasMaxLength(100);
            parcel.Property(p => p.WeightKg).HasPrecision(18, 3);
            parcel.Property(p => p.Courier).HasMaxLength(32);
            parcel.Property(p => p.Vehicle).HasMaxLength(32);
            parcel.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
            parcel.Property(p => p.TrackingCode).HasMaxLength(64);
            parcel.Property(p => p.FailureReason).HasMaxLength(500);
            parcel.Property<decimal>("_cashToCollectMzn").HasColumnName("CashToCollectMzn").HasPrecision(18, 2);
            parcel.Ignore(p => p.CashToCollect);
            parcel.OwnsMany(p => p.Tracking, tracking =>
            {
                tracking.ToTable("ParcelTracking");
                tracking.WithOwner().HasForeignKey("ParcelId");
                tracking.Property<int>("Id");
                tracking.HasKey("Id");
                tracking.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
                tracking.Property(t => t.Detail).HasMaxLength(500);
            });
            parcel.Navigation(p => p.Tracking).HasField("_tracking").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
