using Microsoft.EntityFrameworkCore;

namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

public static class InboxModelBuilderExtensions
{
    /// <summary>Maps the inbox table; the primary key resolves concurrent duplicate deliveries.</summary>
    public static ModelBuilder AddInbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<InboxRecord>(inbox =>
        {
            inbox.ToTable("InboxRecords");
            inbox.HasKey(r => r.IdempotencyKey);
            inbox.Property(r => r.CommandName).HasMaxLength(200);
        });

        return modelBuilder;
    }
}
