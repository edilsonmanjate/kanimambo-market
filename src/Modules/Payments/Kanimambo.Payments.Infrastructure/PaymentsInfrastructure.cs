using Kanimambo.Payments.Application;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Contracts;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Infrastructure.Outbox;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Payments.Infrastructure;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInbox();
        modelBuilder.AddOutbox();

        modelBuilder.Entity<Payment>(payment =>
        {
            payment.ToTable("Payments");
            payment.HasKey(p => p.Id);
            payment.HasIndex(p => new { p.OrderId, p.Attempt }).IsUnique();
            payment.HasIndex(p => new { p.Status, p.PinExpiresAt });
            payment.Property(p => p.Method).HasConversion<string>().HasMaxLength(32);
            payment.Property(p => p.Operator).HasConversion<string>().HasMaxLength(16);
            payment.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
            payment.Property(p => p.FailureReason).HasConversion<string>().HasMaxLength(32);
            payment.Property(p => p.Phone).HasMaxLength(20);
            payment.Property(p => p.OperatorTransactionId).HasMaxLength(100);
            payment.Property(p => p.NativeCode).HasMaxLength(32);
            payment.Property<decimal>("_amountMzn").HasColumnName("AmountMzn").HasPrecision(18, 2);
            payment.OwnsMany(p => p.History, history =>
            {
                history.ToTable("PaymentTransitions");
                history.WithOwner().HasForeignKey("PaymentId");
                history.Property<int>("Id");
                history.HasKey("Id");
                history.Property(h => h.From).HasConversion<string>().HasMaxLength(32);
                history.Property(h => h.To).HasConversion<string>().HasMaxLength(32);
                history.Property(h => h.Detail).HasMaxLength(500);
            });
            payment.Navigation(p => p.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
            payment.Ignore(p => p.Amount);
            payment.Ignore(p => p.State);
            payment.Ignore(p => p.DomainEvents);
        });
    }
}

internal sealed class PaymentsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", PaymentsDbContext.Schema);
        return new PaymentsDbContext(options.Options);
    }
}

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

internal sealed class PaymentsIntegrationEventMapper : IIntegrationEventMapper
{
    public IEnumerable<IIntegrationEvent> Map(IDomainEvent domainEvent) => domainEvent switch
    {
        PaymentConfirmed e => [new PaymentConfirmedIntegrationEvent(e.EventId, e.OccurredAt, e.PaymentId, e.OrderId, e.Attempt, e.Reference)],
        PaymentFailed e => [new PaymentFailedIntegrationEvent(e.EventId, e.OccurredAt, e.PaymentId, e.OrderId, e.Attempt, e.Reason.ToString(), e.Detail)],
        _ => [],
    };
}

/// <summary>Local stand-in for the SIMO card network: approves, except amounts ending in ,99 (to exercise declines).</summary>
internal sealed class SimulatedCardAcquirer : ICardAcquirer
{
    public Task<Result<string>> ChargeAsync(Guid paymentId, Money amount, CancellationToken cancellationToken) =>
        Task.FromResult(amount.Amount % 1m == 0.99m
            ? Result<string>.Failure(Error.BusinessRule("payments.card_declined", "The card issuer declined the payment."))
            : Result<string>.Success($"SIMO-{paymentId.ToString("N")[..10].ToUpperInvariant()}"));
}

internal sealed class KeyedGatewayProvider(IServiceProvider services) : IMobileMoneyGatewayProvider
{
    public IMobileMoneyGateway For(MobileOperator mobileOperator) => services.GetRequiredKeyedService<IMobileMoneyGateway>(mobileOperator);
}

/// <summary>Resolves expired PIN prompts every few seconds (asks the operator, then times the payment out).</summary>
internal sealed partial class PaymentTimeoutMonitor(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    PaymentsOptions options,
    ILogger<PaymentTimeoutMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.TimeoutCheckInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpirePendingPayments(), stoppingToken);
                if (result.IsSuccess && result.Value > 0)
                {
                    LogResolved(logger, result.Value);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved {Count} expired mobile-money payment(s)")]
    private static partial void LogResolved(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment timeout check failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
