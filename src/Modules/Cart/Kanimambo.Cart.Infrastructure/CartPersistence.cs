using System.Text.Json;
using Kanimambo.Cart.Application;
using Kanimambo.Cart.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Cart.Infrastructure;

internal sealed class CartRepository(CartDbContext db) : ICartRepository
{
    public Task<ShoppingCart?> GetAsync(CartId id, CancellationToken cancellationToken) =>
        db.Carts.Include(c => c.Lines).SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(ShoppingCart cart) => db.Carts.Add(cart);
}

/// <summary>
/// Caretaker backed by the <c>CartSnapshots</c> table. Mementos are stored as JSON and never inspected here.
/// Writes join the command's unit of work, so a change and its snapshot commit together.
/// </summary>
internal sealed class CartHistory(CartDbContext db) : ICartHistory
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Save(CartId cartId, CartMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        db.Snapshots.Add(new CartSnapshotRecord
        {
            CartId = cartId.Value,
            Version = memento.Version,
            TakenAt = memento.TakenAt,
            Payload = JsonSerializer.Serialize(memento, Json),
        });
    }

    public async Task<IReadOnlyList<CartMemento>> ListAsync(CartId cartId, CancellationToken cancellationToken)
    {
        var payloads = await db.Snapshots.AsNoTracking()
            .Where(s => s.CartId == cartId.Value)
            .OrderBy(s => s.Version)
            .Select(s => s.Payload)
            .ToListAsync(cancellationToken);

        return payloads.Select(p => JsonSerializer.Deserialize<CartMemento>(p, Json)!).ToList();
    }

    public Task DiscardAfterAsync(CartId cartId, int version, CancellationToken cancellationToken) =>
        db.Snapshots.Where(s => s.CartId == cartId.Value && s.Version > version).ExecuteDeleteAsync(cancellationToken);

    public Task PruneAsync(CartId cartId, int newestVersion, int keepLatest, CancellationToken cancellationToken) =>
        db.Snapshots.Where(s => s.CartId == cartId.Value && s.Version <= newestVersion - keepLatest).ExecuteDeleteAsync(cancellationToken);
}
