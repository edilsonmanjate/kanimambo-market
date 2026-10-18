using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Invoicing.Infrastructure;

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
