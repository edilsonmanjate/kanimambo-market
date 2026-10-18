using Kanimambo.Ordering.Domain;

namespace Kanimambo.Ordering.Application;

public interface IOrderRepository
{
    void Add(Order order);

    Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken);

    /// <summary>The order already placed from this cart or draft, if any (checkout idempotency).</summary>
    Task<Order?> FindBySourceAsync(string checkoutSource, Guid sourceId, CancellationToken cancellationToken);

    /// <summary>Non-cancelled orders placed with a phone number since a moment (anti-fraud).</summary>
    Task<(int Count, decimal TotalMzn)> RecentByPhoneAsync(string phone, DateTimeOffset since, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken);
}
