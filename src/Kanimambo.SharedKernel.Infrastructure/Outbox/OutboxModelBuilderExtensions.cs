using Microsoft.EntityFrameworkCore;

namespace Kanimambo.SharedKernel.Infrastructure.Outbox;

public static class OutboxModelBuilderExtensions
{
    public const string TableName = "OutboxMessages";

    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(message =>
        {
            message.ToTable(TableName);
            message.HasKey(m => m.Id);
            message.Property(m => m.Type).HasMaxLength(500);
            message.Property(m => m.Payload).HasColumnType("jsonb");
            message.Property(m => m.LastError).HasMaxLength(4000);
            message.HasIndex(m => new { m.ProcessedAt, m.NextAttemptAt });
        });

        return modelBuilder;
    }
}
