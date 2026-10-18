using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;
using Polly;
using Polly.Retry;

namespace Kanimambo.Payments.Infrastructure.Gateways.Decorators;

/// <summary>
/// Retries transient failures (operator unreachable, 5xx, timeouts) with exponential backoff (Polly v8).
/// Business answers — insufficient balance, invalid number — are never retried. A payment request is safe to
/// retry because its reference is our payment id: if the first attempt did reach the operator, the retry comes
/// back as a duplicate, and the decorator then asks for the status instead of failing the payment.
/// </summary>
public sealed class RetryingGatewayDecorator : MobileMoneyGatewayDecorator
{
    private readonly ResiliencePipeline<Result<GatewayAcceptance>> _requestPipeline;
    private readonly ResiliencePipeline<Result<GatewayPaymentStatus>> _statusPipeline;

    public RetryingGatewayDecorator(IMobileMoneyGateway inner, int maxRetries, TimeSpan baseDelay)
        : base(inner)
    {
        _requestPipeline = Build<GatewayAcceptance>(maxRetries, baseDelay);
        _statusPipeline = Build<GatewayPaymentStatus>(maxRetries, baseDelay);
    }

    public override async Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var attempts = 0;
        var result = await _requestPipeline.ExecuteAsync(async ct =>
        {
            attempts++;
            return await Inner.RequestPaymentAsync(request, ct);
        }, cancellationToken);

        if (attempts > 1 && result.IsFailure && result.Error is GatewayError { Reason: PaymentFailureReason.DuplicateTransaction })
        {
            // An earlier attempt reached the operator after all: recover its transaction instead of failing.
            var status = await Inner.QueryStatusAsync(request.PaymentId, cancellationToken);
            if (status.IsSuccess && status.Value.State != GatewayPaymentState.Failed)
            {
                return new GatewayAcceptance(status.Value.OperatorTransactionId);
            }
        }

        return result;
    }

    public override async Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken) =>
        await _statusPipeline.ExecuteAsync(async ct => await Inner.QueryStatusAsync(paymentId, ct), cancellationToken);

    private static ResiliencePipeline<Result<T>> Build<T>(int maxRetries, TimeSpan baseDelay) =>
        new ResiliencePipelineBuilder<Result<T>>()
            .AddRetry(new RetryStrategyOptions<Result<T>>
            {
                MaxRetryAttempts = maxRetries,
                Delay = baseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false,
                ShouldHandle = new PredicateBuilder<Result<T>>().HandleResult(r => r.IsFailure && r.Error is GatewayError { IsTransient: true }),
            })
            .Build();
}
