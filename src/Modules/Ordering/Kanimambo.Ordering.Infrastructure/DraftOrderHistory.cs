using System.Text.Json;
using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Ordering.Infrastructure;

internal sealed class DraftOrderHistory(OrderingDbContext db) : IDraftOrderHistory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Save(Guid draftId, DraftOrderMemento memento) =>
        db.DraftSnapshots.Add(new DraftSnapshotRecord { DraftId = draftId, Version = memento.Version, Payload = JsonSerializer.Serialize(memento, Json) });

    public async Task<IReadOnlyList<DraftOrderMemento>> ListAsync(Guid draftId, CancellationToken cancellationToken) =>
        (await db.DraftSnapshots.AsNoTracking().Where(s => s.DraftId == draftId).OrderBy(s => s.Version).Select(s => s.Payload).ToListAsync(cancellationToken))
        .Select(p => JsonSerializer.Deserialize<DraftOrderMemento>(p, Json)!)
        .ToList();

    public Task DiscardAfterAsync(Guid draftId, int version, CancellationToken cancellationToken) =>
        db.DraftSnapshots.Where(s => s.DraftId == draftId && s.Version > version).ExecuteDeleteAsync(cancellationToken);
}
