using Kanimambo.Cart.Domain;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kanimambo.Cart.Infrastructure;

/// <summary>A stored snapshot: the serialized memento, opaque to the database.</summary>
internal sealed class CartSnapshotRecord
{
    public required Guid CartId { get; init; }

    public required int Version { get; init; }

    public required DateTimeOffset TakenAt { get; init; }

    public required string Payload { get; init; }
}

public sealed class CartDbContext(DbContextOptions<CartDbContext> options) : DbContext(options)
{
    public const string Schema = "cart";

    private static readonly ValueConverter<CartId, Guid> CartIdConverter = new(id => id.Value, value => new CartId(value));

    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();

    internal DbSet<CartSnapshotRecord> Snapshots => Set<CartSnapshotRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();

        modelBuilder.Entity<ShoppingCart>(cart =>
        {
            cart.ToTable("Carts");
            cart.HasKey(c => c.Id);
            cart.Property(c => c.Id).HasConversion(CartIdConverter);
            cart.Property<string?>("_customerId").HasColumnName("CustomerId").HasMaxLength(200);
            cart.Property<string?>("_customerType").HasColumnName("CustomerType").HasMaxLength(16);
            cart.Property(c => c.DeliveryCity).HasMaxLength(100);

            // Optimistic concurrency on PostgreSQL's xmin: two devices editing the same cart cannot overwrite each other.
            cart.Property<uint>("RowVersion").IsRowVersion();

            cart.HasMany(c => c.Lines).WithOne().HasForeignKey("CartId").OnDelete(DeleteBehavior.Cascade);
            cart.Navigation(c => c.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
            cart.Ignore(c => c.Customer);
            cart.Ignore(c => c.ItemCount);
            cart.Ignore(c => c.IsCheckedOut);
            cart.Ignore(c => c.DomainEvents);
        });

        modelBuilder.Entity<CartLine>(line =>
        {
            line.ToTable("CartLines");
            line.Property<CartId>("CartId").HasConversion(CartIdConverter);
            line.HasKey("CartId", nameof(CartLine.ProductId));
            line.Property(l => l.Sku).HasMaxLength(64);
            line.Property(l => l.Name).HasMaxLength(300);
        });

        modelBuilder.Entity<CartSnapshotRecord>(snapshot =>
        {
            snapshot.ToTable("CartSnapshots");
            snapshot.HasKey(s => new { s.CartId, s.Version });
            snapshot.Property(s => s.Payload).HasColumnType("jsonb");
        });
    }
}

internal sealed class CartDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CartDbContext>
{
    public CartDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", CartDbContext.Schema);
        return new CartDbContext(options.Options);
    }
}
