using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Kanimambo.Api.Idempotency;

internal sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) : DbContext(options)
{
    public const string Schema = "platform";
    public const int MaxKeyLength = 128;

    public DbSet<IdempotencyRecord> Records => Set<IdempotencyRecord>();

    public static void Configure(DbContextOptionsBuilder options, string? connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var record = modelBuilder.Entity<IdempotencyRecord>();
        record.ToTable("IdempotencyRecords");
        record.HasKey(r => new { r.Scope, r.Key });
        record.Property(r => r.Scope).HasMaxLength(512);
        record.Property(r => r.Key).HasMaxLength(MaxKeyLength);
        record.Property(r => r.RequestFingerprint).HasMaxLength(64).IsFixedLength();
        record.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        record.Property(r => r.ResponseContentType).HasMaxLength(256);
        record.Property(r => r.ResponseLocation).HasMaxLength(2048);
        record.HasIndex(r => r.ExpiresAt);
    }
}

/// <summary>Lets <c>dotnet ef</c> build the model without Aspire or a running database.</summary>
internal sealed class IdempotencyDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdempotencyDbContext>
{
    public IdempotencyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdempotencyDbContext>();
        IdempotencyDbContext.Configure(options, "Host=localhost;Database=kanimambo");
        return new IdempotencyDbContext(options.Options);
    }
}
