using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Application.Gateways;

/// <summary>Card payments through the SIMO network. Locally a simulated acquirer.</summary>
public interface ICardAcquirer
{
    Task<Result<string>> ChargeAsync(Guid paymentId, Money amount, CancellationToken cancellationToken);
}
