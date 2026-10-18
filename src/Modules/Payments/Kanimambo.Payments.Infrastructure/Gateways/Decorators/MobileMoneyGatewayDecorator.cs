using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Infrastructure.Gateways.Decorators;

/// <summary>
/// Decorator base: same interface as the adapter it wraps, forwarding everything by default. Each concrete
/// decorator adds one concern without the adapter (or the others) knowing.
/// </summary>
public abstract class MobileMoneyGatewayDecorator(IMobileMoneyGateway inner) : IMobileMoneyGateway
{
    protected IMobileMoneyGateway Inner { get; } = inner;

    public MobileOperator Operator => Inner.Operator;

    public virtual Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken) =>
        Inner.RequestPaymentAsync(request, cancellationToken);

    public virtual Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken) =>
        Inner.QueryStatusAsync(paymentId, cancellationToken);

    public virtual Result<GatewayPaymentStatus> ParseCallback(string body, string? signature) => Inner.ParseCallback(body, signature);
}
