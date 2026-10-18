using Kanimambo.Payments.Application.Gateways;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Infrastructure;

/// <summary>Local stand-in for the SIMO card network: approves, except amounts ending in ,99 (to exercise declines).</summary>
internal sealed class SimulatedCardAcquirer : ICardAcquirer
{
    public Task<Result<string>> ChargeAsync(Guid paymentId, Money amount, CancellationToken cancellationToken) =>
        Task.FromResult(amount.Amount % 1m == 0.99m
            ? Result<string>.Failure(Error.BusinessRule("payments.card_declined", "The card issuer declined the payment."))
            : Result<string>.Success($"SIMO-{paymentId.ToString("N")[..10].ToUpperInvariant()}"));
}
