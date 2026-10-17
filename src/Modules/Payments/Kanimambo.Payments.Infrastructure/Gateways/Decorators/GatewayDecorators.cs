using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

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

/// <summary>Structured log line per operator call: operation, outcome, duration.</summary>
public sealed partial class LoggingGatewayDecorator(IMobileMoneyGateway inner, ILogger<LoggingGatewayDecorator> logger) : MobileMoneyGatewayDecorator(inner)
{
    public override async Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = Stopwatch.GetTimestamp();
        var result = await Inner.RequestPaymentAsync(request, cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var outcome = Outcome(result);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Log(logger, Operator, "RequestPayment", request.PaymentId, outcome, elapsed);
        }

        return result;
    }

    public override async Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await Inner.QueryStatusAsync(paymentId, cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var outcome = result.IsSuccess ? result.Value.State.ToString() : Outcome(result);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Log(logger, Operator, "QueryStatus", paymentId, outcome, elapsed);
        }

        return result;
    }

    public override Result<GatewayPaymentStatus> ParseCallback(string body, string? signature)
    {
        var result = Inner.ParseCallback(body, signature);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var paymentId = result.IsSuccess ? result.Value.PaymentId : Guid.Empty;
            var outcome = result.IsSuccess ? result.Value.State.ToString() : Outcome(result);
            Log(logger, Operator, "Callback", paymentId, outcome, 0);
        }

        return result;
    }

    internal static string Outcome(Result result) => result.IsSuccess ? "ok" : result.Error.Code;

    [LoggerMessage(Level = LogLevel.Information, Message = "{Operator} {Operation} for payment {PaymentId}: {Outcome} in {ElapsedMs:0.0} ms")]
    private static partial void Log(ILogger logger, MobileOperator @operator, string operation, Guid paymentId, string outcome, double elapsedMs);
}

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

public sealed class GatewayMetrics : IDisposable
{
    public const string MeterName = "Kanimambo.Payments";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _calls;
    private readonly Histogram<double> _duration;

    public GatewayMetrics()
    {
        _calls = _meter.CreateCounter<long>("kanimambo.payments.gateway.calls", description: "Calls to mobile-money operators");
        _duration = _meter.CreateHistogram<double>("kanimambo.payments.gateway.duration", "ms", "Latency of mobile-money operator calls");
    }

    public void Record(MobileOperator mobileOperator, string operation, string outcome, TimeSpan elapsed)
    {
        var tags = new TagList { { "operator", mobileOperator.ToString() }, { "operation", operation }, { "outcome", outcome } };
        _calls.Add(1, tags);
        _duration.Record(elapsed.TotalMilliseconds, tags);
    }

    public void Dispose() => _meter.Dispose();
}

/// <summary>
/// Caches <em>final</em> statuses (confirmed / failed) in Redis: a settled transaction never changes, and
/// operators rate-limit status queries. Pending statuses are never cached.
/// </summary>
public sealed class CachingGatewayDecorator(IMobileMoneyGateway inner, IDistributedCache cache) : MobileMoneyGatewayDecorator(inner)
{
    private static readonly DistributedCacheEntryOptions Lifetime = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) };

    public override async Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"payments:status:{Operator}:{paymentId}");
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached)
        {
            return JsonSerializer.Deserialize<GatewayPaymentStatus>(cached)!;
        }

        var result = await Inner.QueryStatusAsync(paymentId, cancellationToken);
        if (result.IsSuccess && result.Value.IsFinal)
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(result.Value), Lifetime, cancellationToken);
        }

        return result;
    }
}

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
