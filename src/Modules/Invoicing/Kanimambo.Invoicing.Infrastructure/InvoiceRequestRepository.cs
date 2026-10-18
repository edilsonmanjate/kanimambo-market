using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Invoicing.Infrastructure;

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
