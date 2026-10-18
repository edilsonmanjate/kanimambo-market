using Kanimambo.Ordering.Application;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Ordering.Infrastructure;

/// <summary>Order numbers from a PostgreSQL sequence: unique, gap-tolerant, never reused.</summary>
internal sealed class OrderNumbers(OrderingDbContext db) : IOrderNumbers
{
    public Task<long> NextAsync(CancellationToken cancellationToken) =>
        db.Database.SqlQueryRaw<long>($"SELECT nextval('{OrderingDbContext.Schema}.\"{OrderingDbContext.OrderNumberSequence}\"') AS \"Value\"")
            .SingleAsync(cancellationToken);
}
