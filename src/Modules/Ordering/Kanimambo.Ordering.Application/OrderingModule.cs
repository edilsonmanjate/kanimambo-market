using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application;

public static class OrderingModule
{
    public const string Name = "Ordering";
}

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

public interface IDraftOrderRepository
{
    void Add(DraftOrder draft);

    Task<DraftOrder?> GetAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Caretaker for draft snapshots (same linear-history model as carts).</summary>
public interface IDraftOrderHistory
{
    void Save(Guid draftId, DraftOrderMemento memento);

    Task<IReadOnlyList<DraftOrderMemento>> ListAsync(Guid draftId, CancellationToken cancellationToken);

    Task DiscardAfterAsync(Guid draftId, int version, CancellationToken cancellationToken);
}

public interface IOrderNumbers
{
    Task<long> NextAsync(CancellationToken cancellationToken);
}

/// <summary>Limits per phone number in a rolling 24 h window. Values come from configuration.</summary>
public sealed record AntiFraudSettings(int MaxOrdersPerPhonePerDay, decimal MaxValuePerPhonePerDayMzn);

internal static class OrderingErrors
{
    public static Error OrderNotFound(Guid id) => Error.NotFound("ordering.order_not_found", $"Order {id} does not exist.");

    public static Error DraftNotFound(Guid id) => Error.NotFound("ordering.draft_not_found", $"Draft {id} does not exist.");
}

internal static class OrderAccess
{
    /// <summary>Back-office sees everything; customers their own orders; guest orders are reached by their random id.</summary>
    public static bool CanView(Order order, ICurrentActor actor) =>
        actor.IsInRole(KnownRoles.Backoffice)
        || order.Customer.Id.Length == 0
        || (actor.IsAuthenticated && order.Customer.Id == actor.Name);

    public static bool CanEdit(DraftOrder draft, ICurrentActor actor) =>
        actor.IsInRole(KnownRoles.Backoffice) || (actor.IsAuthenticated && draft.CustomerId == actor.Name);
}
