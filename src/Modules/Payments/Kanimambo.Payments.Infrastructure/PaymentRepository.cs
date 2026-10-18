using Kanimambo.Payments.Application;
using Kanimambo.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Payments.Infrastructure;

internal sealed class PaymentRepository(PaymentsDbContext db) : IPaymentRepository
{
    public void Add(Payment payment) => db.Payments.Add(payment);

    public Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Payments.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Payment?> FindByOrderAttemptAsync(Guid orderId, int attempt, CancellationToken cancellationToken) =>
        db.Payments.SingleOrDefaultAsync(p => p.OrderId == orderId && p.Attempt == attempt, cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.Payments.AsNoTracking().Where(p => p.OrderId == orderId).OrderBy(p => p.Attempt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListExpiredAwaitingPinAsync(DateTimeOffset now, int max, CancellationToken cancellationToken) =>
        await db.Payments
            .Where(p => p.Status == PaymentStatus.AwaitingCustomerPin && p.PinExpiresAt <= now && p.Operator != null)
            .OrderBy(p => p.PinExpiresAt)
            .Take(max)
            .ToListAsync(cancellationToken);
}
