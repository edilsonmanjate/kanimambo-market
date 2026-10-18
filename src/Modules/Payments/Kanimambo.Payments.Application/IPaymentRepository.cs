using Kanimambo.Payments.Domain;

namespace Kanimambo.Payments.Application;

public interface IPaymentRepository
{
    void Add(Payment payment);

    Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Payment?> FindByOrderAttemptAsync(Guid orderId, int attempt, CancellationToken cancellationToken);

    Task<IReadOnlyList<Payment>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Mobile-money payments still waiting for the customer's PIN after their deadline.</summary>
    Task<IReadOnlyList<Payment>> ListExpiredAwaitingPinAsync(DateTimeOffset now, int max, CancellationToken cancellationToken);
}
