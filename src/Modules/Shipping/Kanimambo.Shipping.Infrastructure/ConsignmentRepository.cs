using Kanimambo.Shipping.Application;
using Kanimambo.Shipping.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Shipping.Infrastructure;

internal sealed class ConsignmentRepository(ShippingDbContext db) : IConsignmentRepository
{
    public void Add(Consignment consignment) => db.Consignments.Add(consignment);

    public Task<Consignment?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Consignments.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Consignment?> FindByOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Consignments.SingleOrDefaultAsync(c => c.OrderId == orderId, cancellationToken);

    public Task<Consignment?> FindByParcelReferenceAsync(string reference, CancellationToken cancellationToken) =>
        db.Consignments.SingleOrDefaultAsync(c => c.Parcels.Any(p => p.Reference == reference), cancellationToken);
}
