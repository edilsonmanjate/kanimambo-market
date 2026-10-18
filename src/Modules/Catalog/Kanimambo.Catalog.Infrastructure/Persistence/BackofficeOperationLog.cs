using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Persistence;

internal sealed class BackofficeOperationLog(CatalogDbContext db) : IBackofficeOperationLog
{
    public void Add(BackofficeOperationRecord record) => db.BackofficeOperations.Add(record);

    public Task<BackofficeOperationRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.BackofficeOperations.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<PagedResult<BackofficeOperationRecord>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await db.BackofficeOperations.CountAsync(cancellationToken);
        var items = await db.BackofficeOperations.AsNoTracking()
            .OrderByDescending(o => o.PerformedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BackofficeOperationRecord>(items, page, pageSize, total);
    }
}
