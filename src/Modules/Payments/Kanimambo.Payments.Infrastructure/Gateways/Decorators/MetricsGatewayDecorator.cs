using System.Diagnostics;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Infrastructure.Gateways.Decorators;

/// <summary>OpenTelemetry metrics: calls and latency per operator, operation and outcome.</summary>
public sealed class MetricsGatewayDecorator(IMobileMoneyGateway inner, GatewayMetrics metrics) : MobileMoneyGatewayDecorator(inner)
{
    public override async Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await Inner.RequestPaymentAsync(request, cancellationToken);
        metrics.Record(Operator, "request_payment", LoggingGatewayDecorator.Outcome(result), Stopwatch.GetElapsedTime(started));
        return result;
    }

    public override async Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await Inner.QueryStatusAsync(paymentId, cancellationToken);
        metrics.Record(Operator, "query_status", LoggingGatewayDecorator.Outcome(result), Stopwatch.GetElapsedTime(started));
        return result;
    }
}
