using System.Text.Json;
using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Ordering.Infrastructure;

internal sealed class OrderRepository(OrderingDbContext db) : IOrderRepository
{
    public void Add(Order order) => db.Orders.Add(order);

    public Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken) =>
        db.Orders.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Order?> FindBySourceAsync(string checkoutSource, Guid sourceId, CancellationToken cancellationToken) =>
        db.Orders.SingleOrDefaultAsync(o => o.CheckoutSource == checkoutSource && o.CheckoutSourceId == sourceId, cancellationToken);

    public async Task<(int Count, decimal TotalMzn)> RecentByPhoneAsync(string phone, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var recent = await db.Orders.AsNoTracking()
            .Where(o => o.Customer.Phone == phone && o.PlacedAt >= since && o.Status != OrderStatus.Cancelled)
            .Select(o => EF.Property<decimal>(o, "_subtotal") - EF.Property<decimal>(o, "_discount") + EF.Property<decimal>(o, "_shippingFee"))
            .ToListAsync(cancellationToken);
        return (recent.Count, recent.Sum());
    }

    public async Task<IReadOnlyList<Order>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking().Where(o => o.Customer.Id == customerId).OrderByDescending(o => o.PlacedAt).ToListAsync(cancellationToken);
}

internal sealed class DraftOrderRepository(OrderingDbContext db) : IDraftOrderRepository
{
    public void Add(DraftOrder draft) => db.Drafts.Add(draft);

    public Task<DraftOrder?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Drafts.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
}

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

/// <summary>Order numbers from a PostgreSQL sequence: unique, gap-tolerant, never reused.</summary>
internal sealed class OrderNumbers(OrderingDbContext db) : IOrderNumbers
{
    public Task<long> NextAsync(CancellationToken cancellationToken) =>
        db.Database.SqlQueryRaw<long>($"SELECT nextval('{OrderingDbContext.Schema}.\"{OrderingDbContext.OrderNumberSequence}\"') AS \"Value\"")
            .SingleAsync(cancellationToken);
}
