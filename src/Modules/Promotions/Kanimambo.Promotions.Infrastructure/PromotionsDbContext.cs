using Kanimambo.Promotions.Application;
using Kanimambo.Promotions.Domain;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanimambo.Promotions.Infrastructure;

public sealed class PromotionsDbContext(DbContextOptions<PromotionsDbContext> options) : DbContext(options)
{
    public const string Schema = "promotions";

    public DbSet<Promotion> Promotions => Set<Promotion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();

        modelBuilder.Entity<Promotion>(promotion =>
        {
            promotion.ToTable("Promotions");
            promotion.HasKey(p => p.Id);
            promotion.Property(p => p.Name).HasMaxLength(200);
            promotion.Property(p => p.RuleText).HasMaxLength(2000);
            promotion.Property(p => p.CreatedBy).HasMaxLength(200);
            promotion.ComplexProperty(p => p.Discount, discount =>
            {
                discount.Property(d => d.Kind).HasColumnName("DiscountKind").HasConversion<string>().HasMaxLength(16);
                discount.Property(d => d.Value).HasColumnName("DiscountValue").HasPrecision(18, 2);
            });
            promotion.HasIndex(p => p.IsActive);
            promotion.Ignore(p => p.Rule);
            promotion.Ignore(p => p.DomainEvents);
        });
    }
}

internal sealed class PromotionRepository(PromotionsDbContext db) : IPromotionRepository
{
    public void Add(Promotion promotion) => db.Promotions.Add(promotion);

    public Task<Promotion?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Promotions.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Promotion>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
        await db.Promotions.Where(p => !activeOnly || p.IsActive).OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken);
}

internal sealed class PromotionsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PromotionsDbContext>
{
    public PromotionsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", PromotionsDbContext.Schema);
        return new PromotionsDbContext(options.Options);
    }
}
