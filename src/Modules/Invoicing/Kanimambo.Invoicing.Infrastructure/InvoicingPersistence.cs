using System.Text.Json;
using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Infrastructure.Outbox;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanimambo.Invoicing.Infrastructure;

public sealed class InvoicingDbContext(DbContextOptions<InvoicingDbContext> options) : DbContext(options)
{
    public const string Schema = "invoicing";

    public DbSet<FiscalDocument> Documents => Set<FiscalDocument>();

    public DbSet<TaxpayerProfile> Taxpayers => Set<TaxpayerProfile>();

    public DbSet<InvoiceRequest> InvoiceRequests => Set<InvoiceRequest>();

    public DbSet<DocumentCounter> Counters => Set<DocumentCounter>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();
        modelBuilder.AddOutbox();

        modelBuilder.Entity<FiscalDocument>(document =>
        {
            document.ToTable("FiscalDocuments");
            document.HasKey(d => d.Id);
            document.HasDiscriminator(d => d.Type)
                .HasValue<Invoice>(DocumentType.Invoice)
                .HasValue<CreditNote>(DocumentType.CreditNote);
            document.Property(d => d.Type).HasConversion<string>().HasMaxLength(16);
            document.Property(d => d.Regime).HasConversion<string>().HasMaxLength(16);
            document.Property(d => d.Series).HasMaxLength(16);
            document.Property(d => d.FullNumber).HasMaxLength(40);
            document.Property(d => d.Title).HasMaxLength(40);
            document.Property(d => d.LegalNotice).HasMaxLength(500);
            document.Property(d => d.IssuerNuit).HasMaxLength(9);
            document.Property(d => d.CustomerPhone).HasMaxLength(20);
            document.Property(d => d.Net).HasPrecision(18, 2);
            document.Property(d => d.Iva).HasPrecision(18, 2);
            document.Property(d => d.Gross).HasPrecision(18, 2);

            // Fiscal numbering: one number per issuer, series and year — never twice.
            document.HasIndex(d => new { d.IssuerNuit, d.Series, d.Year, d.Number }).IsUnique();
            document.HasIndex(d => new { d.IssuerNuit, d.IssueDate });
            document.HasIndex(d => d.OrderId);

            document.OwnsOne(d => d.Issuer, party => MapParty(party, "Issuer"));
            document.OwnsOne(d => d.Buyer, party => MapParty(party, "Buyer"));
            document.Navigation(d => d.Issuer).IsRequired();
            document.Navigation(d => d.Buyer).IsRequired();

            document.OwnsMany(d => d.Lines, line =>
            {
                line.ToTable("FiscalLines");
                line.WithOwner().HasForeignKey("DocumentId");
                line.Property<int>("Id");
                line.HasKey("Id");
                line.Property(l => l.Description).HasMaxLength(300);
                line.Property(l => l.Sku).HasMaxLength(64);
                line.Property(l => l.TaxCategory).HasMaxLength(32);
                line.Property(l => l.ExemptionReason).HasMaxLength(300);
                line.Property(l => l.Quantity).HasPrecision(18, 3);
                line.Property(l => l.Rate).HasPrecision(5, 4);
                foreach (var amount in new[] { nameof(FiscalLine.Discount), nameof(FiscalLine.Net), nameof(FiscalLine.Iva), nameof(FiscalLine.Gross) })
                {
                    line.Property(amount).HasPrecision(18, 2);
                }
            });
            document.Navigation(d => d.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
            document.Ignore(d => d.DomainEvents);
        });

        modelBuilder.Entity<CreditNote>(note =>
        {
            note.Property(c => c.CreditedInvoiceNumber).HasMaxLength(40);
            note.Property(c => c.Reason).HasMaxLength(500);
            note.HasIndex(c => c.CreditedInvoiceId);
        });

        modelBuilder.Entity<TaxpayerProfile>(profile =>
        {
            profile.ToTable("Taxpayers");
            profile.HasKey(p => p.Id);
            profile.Property(p => p.Id).ValueGeneratedNever();
            profile.HasIndex(p => p.Nuit).IsUnique();
            profile.Property(p => p.Nuit).HasMaxLength(9);
            profile.Property(p => p.LegalName).HasMaxLength(200);
            profile.Property(p => p.Address).HasMaxLength(300);
            profile.Property(p => p.Regime).HasConversion<string>().HasMaxLength(16);
            profile.Ignore(p => p.AsIssuer);
            profile.Ignore(p => p.DomainEvents);
        });

        modelBuilder.Entity<InvoiceRequest>(request =>
        {
            request.ToTable("InvoiceRequests");
            request.HasKey(r => r.Id);
            request.HasIndex(r => new { r.OrderId, r.SellerId }).IsUnique();
            request.HasIndex(r => new { r.SellerId, r.Status });
            request.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
            request.Property(r => r.CustomerPhone).HasMaxLength(20);
            request.Property(r => r.Problem).HasMaxLength(1000);
            request.OwnsOne(r => r.Buyer, party => MapParty(party, "Buyer"));
            request.Navigation(r => r.Buyer).IsRequired();

            // The sale as it was when the order was paid, kept until the invoice can be issued.
            request.Property(r => r.Lines)
                .HasColumnType("jsonb")
                .HasConversion(
                    lines => JsonSerializer.Serialize(lines, JsonSerializerOptions.Web),
                    json => JsonSerializer.Deserialize<List<LineToPrice>>(json, JsonSerializerOptions.Web) ?? new List<LineToPrice>(),
                    new ValueComparer<IReadOnlyList<LineToPrice>>(
                        (a, b) => a != null && b != null && a.SequenceEqual(b),
                        lines => lines.Aggregate(0, (hash, line) => HashCode.Combine(hash, line.GetHashCode())),
                        lines => lines.ToList()));
            request.Ignore(r => r.DomainEvents);
        });

        modelBuilder.Entity<DocumentCounter>(counter =>
        {
            counter.ToTable("DocumentCounters");
            counter.HasKey(c => new { c.IssuerNuit, c.Series, c.Year });
            counter.Property(c => c.IssuerNuit).HasMaxLength(9);
            counter.Property(c => c.Series).HasMaxLength(16);
            counter.Property<uint>("RowVersion").IsRowVersion();
        });
    }

    private static void MapParty<TOwner>(OwnedNavigationBuilder<TOwner, FiscalParty> party, string prefix)
        where TOwner : class
    {
        party.Property(p => p.Nuit).HasColumnName($"{prefix}Nuit").HasMaxLength(9);
        party.Property(p => p.Name).HasColumnName($"{prefix}Name").HasMaxLength(200);
        party.Property(p => p.Address).HasColumnName($"{prefix}Address").HasMaxLength(300);
    }
}

internal sealed class InvoicingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<InvoicingDbContext>
{
    public InvoicingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InvoicingDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", InvoicingDbContext.Schema);
        return new InvoicingDbContext(options.Options);
    }
}

internal sealed class FiscalDocumentRepository(InvoicingDbContext db) : IFiscalDocumentRepository
{
    public void Add(FiscalDocument document) => db.Documents.Add(document);

    public Task<FiscalDocument?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Documents.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FiscalDocument>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.Documents.AsNoTracking().Where(d => d.OrderId == orderId).OrderBy(d => d.Type).ThenBy(d => d.CreatedAt).ToListAsync(cancellationToken);

    public Task<bool> IsCreditedAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        db.Documents.OfType<CreditNote>().AnyAsync(c => c.CreditedInvoiceId == invoiceId, cancellationToken);

    public async Task<IReadOnlyList<FiscalDocument>> ListForPeriodAsync(string issuerNuit, int year, int month, CancellationToken cancellationToken)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        return await db.Documents.AsNoTracking()
            .Where(d => d.IssuerNuit == issuerNuit && d.IssueDate >= from && d.IssueDate < to)
            .ToListAsync(cancellationToken);
    }
}

internal sealed class TaxpayerRepository(InvoicingDbContext db) : ITaxpayerRepository
{
    public void Add(TaxpayerProfile profile) => db.Taxpayers.Add(profile);

    public Task<TaxpayerProfile?> GetAsync(Guid sellerId, CancellationToken cancellationToken) =>
        db.Taxpayers.SingleOrDefaultAsync(p => p.Id == sellerId, cancellationToken);

    public Task<TaxpayerProfile?> FindByNuitAsync(string nuit, CancellationToken cancellationToken) =>
        db.Taxpayers.SingleOrDefaultAsync(p => p.Nuit == nuit, cancellationToken);
}

internal sealed class InvoiceRequestRepository(InvoicingDbContext db) : IInvoiceRequestRepository
{
    public void Add(InvoiceRequest request) => db.InvoiceRequests.Add(request);

    public async Task<IReadOnlyList<InvoiceRequest>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.InvoiceRequests.Where(r => r.OrderId == orderId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InvoiceRequest>> ListAwaitingAsync(Guid sellerId, CancellationToken cancellationToken) =>
        await db.InvoiceRequests.Where(r => r.SellerId == sellerId && r.Status == InvoiceRequestStatus.AwaitingTaxpayer)
            .OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InvoiceRequest>> ListByStatusAsync(InvoiceRequestStatus status, CancellationToken cancellationToken) =>
        await db.InvoiceRequests.AsNoTracking().Where(r => r.Status == status).OrderBy(r => r.CreatedAt).Take(500).ToListAsync(cancellationToken);
}

/// <summary>The counter row is tracked and saved with the document, so a number is used if and only if the document is saved.</summary>
internal sealed class DocumentNumbering(InvoicingDbContext db) : IDocumentNumbering
{
    public async Task<DocumentCounter> CounterAsync(string issuerNuit, string series, int year, CancellationToken cancellationToken)
    {
        var counter = db.Counters.Local.SingleOrDefault(c => c.IssuerNuit == issuerNuit && c.Series == series && c.Year == year)
            ?? await db.Counters.SingleOrDefaultAsync(c => c.IssuerNuit == issuerNuit && c.Series == series && c.Year == year, cancellationToken);
        if (counter is null)
        {
            counter = new DocumentCounter(issuerNuit, series, year);
            db.Counters.Add(counter);
        }

        return counter;
    }
}

internal sealed class InvoicingIntegrationEventMapper : IIntegrationEventMapper
{
    public IEnumerable<IIntegrationEvent> Map(IDomainEvent domainEvent) => domainEvent switch
    {
        FiscalDocumentIssued e => [new FiscalDocumentIssuedIntegrationEvent(e.EventId, e.OccurredAt, e.DocumentId, e.Type.ToString(), e.Number, e.OrderId,
            e.OrderNumber, e.IssuerNuit, e.IssuerName, e.TotalMzn, e.CustomerPhone)],
        _ => [],
    };
}
