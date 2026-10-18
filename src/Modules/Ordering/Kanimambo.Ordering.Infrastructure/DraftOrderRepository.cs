using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Ordering.Infrastructure;

internal sealed class DraftOrderRepository(OrderingDbContext db) : IDraftOrderRepository
{
    public void Add(DraftOrder draft) => db.Drafts.Add(draft);

    public Task<DraftOrder?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Drafts.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
}
