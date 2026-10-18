using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Invoicing.Infrastructure;

internal sealed class TaxpayerRepository(InvoicingDbContext db) : ITaxpayerRepository
{
    public void Add(TaxpayerProfile profile) => db.Taxpayers.Add(profile);

    public Task<TaxpayerProfile?> GetAsync(Guid sellerId, CancellationToken cancellationToken) =>
        db.Taxpayers.SingleOrDefaultAsync(p => p.Id == sellerId, cancellationToken);

    public Task<TaxpayerProfile?> FindByNuitAsync(string nuit, CancellationToken cancellationToken) =>
        db.Taxpayers.SingleOrDefaultAsync(p => p.Nuit == nuit, cancellationToken);
}
