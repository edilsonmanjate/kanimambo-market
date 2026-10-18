using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Application.Gateways;

/// <summary>
/// Adapter target: the one interface the application uses for every mobile-money operator. M-Pesa, e-Mola
/// and mKesh speak different formats, codes and signatures; each has an adapter that translates to this.
/// </summary>
public interface IMobileMoneyGateway
{
    MobileOperator Operator { get; }

    /// <summary>Asks the operator to push a PIN prompt. Failures are <see cref="GatewayError"/>s.</summary>
    Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken);

    Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>Verifies the operator's signature and translates its callback payload.</summary>
    Result<GatewayPaymentStatus> ParseCallback(string body, string? signature);
}
