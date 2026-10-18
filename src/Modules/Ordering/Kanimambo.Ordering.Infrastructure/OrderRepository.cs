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
