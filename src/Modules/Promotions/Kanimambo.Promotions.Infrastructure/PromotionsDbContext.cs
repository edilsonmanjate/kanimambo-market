using Kanimambo.Promotions.Domain;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
