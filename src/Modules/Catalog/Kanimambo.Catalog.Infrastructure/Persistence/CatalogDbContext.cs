using Kanimambo.Catalog.Application.Search;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kanimambo.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";
    public const string CatalogEntriesView = "catalog_entries";

    private static readonly ValueConverter<ProductId, Guid> ProductIdConverter = new(id => id.Value, value => new ProductId(value));
    private static readonly ValueConverter<SellerId, Guid> SellerIdConverter = new(id => id.Value, value => new SellerId(value));

    public DbSet<Seller> Sellers => Set<Seller>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<BackofficeOperationRecord> BackofficeOperations => Set<BackofficeOperationRecord>();

    /// <summary>Read model over the <c>catalog_entries</c> view (see the migration for the recursive SQL).</summary>
    public DbSet<CatalogEntry> CatalogEntries => Set<CatalogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();

        ConfigureSellers(modelBuilder);
        ConfigureCatalogItems(modelBuilder);
        ConfigureBackoffice(modelBuilder);
        ConfigureCatalogEntries(modelBuilder);
    }

    private static void ConfigureSellers(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Seller>(seller =>
        {
            seller.ToTable("Sellers");
            seller.HasKey(s => s.Id);
            seller.Property(s => s.Id).HasConversion(SellerIdConverter);
            seller.Property(s => s.Name).HasMaxLength(200);
            seller.Property(s => s.City).HasMaxLength(100);
            seller.Property(s => s.Rating).HasPrecision(3, 2);
            seller.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            seller.Property(s => s.SuspensionReason).HasMaxLength(500);
            seller.PrimitiveCollection<List<string>>("_deliveryCities").HasColumnName("DeliveryCities");
            seller.Property<decimal>("_minimumOrderMzn").HasColumnName("MinimumOrderMzn").HasPrecision(18, 2);
            seller.Ignore(s => s.MinimumOrder);
            seller.Ignore(s => s.DeliveryCities);
            seller.Ignore(s => s.IsActive);
            seller.Ignore(s => s.DomainEvents);
        });

    private static void ConfigureCatalogItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogItem>(item =>
        {
            item.ToTable("CatalogItems");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).HasConversion(ProductIdConverter);
            item.Property(i => i.SellerId).HasConversion(SellerIdConverter);
            item.HasDiscriminator<string>("Kind").HasValue<SimpleProduct>("Simple").HasValue<Bundle>("Bundle");
            item.Property("Kind").HasMaxLength(16);
            item.Property(i => i.Sku).HasMaxLength(CatalogItem.MaxSkuLength);
            item.Property(i => i.Name).HasMaxLength(300);
            item.Property(i => i.Category).HasMaxLength(100);
            item.HasIndex(i => new { i.SellerId, i.Sku }).IsUnique();
            item.HasIndex(i => i.Category);
            item.Ignore(i => i.Price);
            item.Ignore(i => i.WeightKg);
            item.Ignore(i => i.AvailableStock);
            item.Ignore(i => i.DomainEvents);
        });

        modelBuilder.Entity<SimpleProduct>(product =>
        {
            product.ComplexProperty(p => p.UnitPrice, price =>
            {
                price.Property(m => m.Amount).HasColumnName("PriceAmount").HasPrecision(18, 2);
                price.Property(m => m.Currency).HasColumnName("PriceCurrency").HasMaxLength(3)
                    .HasConversion(c => c.Code, code => Currency.FromCode(code).Value);
            });
            product.Property(p => p.UnitWeightKg).HasPrecision(10, 3);
            product.Property(p => p.TaxCategory).HasMaxLength(32)
                .HasConversion(c => c.Code, code => TaxCategory.From(code).Value);

            // Flyweight on materialization: every row with the same attributes gets the same shared instance.
            product.Property(p => p.Variant).HasColumnName("Variant").HasMaxLength(200)
                .HasConversion(v => v.ToStorageString(), s => VariantAttributesPool.Shared.FromStorageString(s));
        });

        modelBuilder.Entity<Bundle>(bundle =>
        {
            bundle.HasMany(b => b.Components).WithOne().HasForeignKey("BundleId").OnDelete(DeleteBehavior.Cascade);
            bundle.Navigation(b => b.Components).HasField("_components").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<BundleComponent>(component =>
        {
            component.ToTable("BundleComponents");
            component.Property<ProductId>("BundleId").HasConversion(ProductIdConverter);
            component.Property(c => c.ComponentId).HasConversion(ProductIdConverter);
            component.HasKey("BundleId", nameof(BundleComponent.ComponentId));
            component.HasOne(c => c.Component).WithMany().HasForeignKey(c => c.ComponentId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBackoffice(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<BackofficeOperationRecord>(operation =>
        {
            operation.ToTable("BackofficeOperations");
            operation.HasKey(o => o.Id);
            operation.Property(o => o.Kind).HasMaxLength(64);
            operation.Property(o => o.Description).HasMaxLength(1000);
            operation.Property(o => o.Payload).HasColumnType("jsonb");
            operation.Property(o => o.PerformedBy).HasMaxLength(200);
            operation.Property(o => o.UndoneBy).HasMaxLength(200);
            operation.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
            operation.HasIndex(o => o.PerformedAt);
        });

    private static void ConfigureCatalogEntries(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<CatalogEntry>(entry =>
        {
            entry.HasNoKey();
            entry.ToView(CatalogEntriesView);
            entry.Property(e => e.Price).HasPrecision(18, 2);
            entry.Property(e => e.SellerRating).HasPrecision(3, 2);
            entry.Property(e => e.WeightKg).HasPrecision(12, 3);
        });
}
