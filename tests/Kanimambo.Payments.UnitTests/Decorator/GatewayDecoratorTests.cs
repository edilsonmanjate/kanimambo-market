using System.Diagnostics.Metrics;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.Payments.Infrastructure.Gateways.Decorators;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kanimambo.Payments.UnitTests.Decorator;

/// <summary>Each decorator in isolation, around a scripted fake operator.</summary>
public sealed class GatewayDecoratorTests
{
    private static readonly GatewayError Unavailable = new(PaymentFailureReason.GatewayUnavailable, null, "503");
    private static readonly GatewayError NoBalance = new(PaymentFailureReason.InsufficientBalance, "INS-6", "No balance");
    private static readonly GatewayError Duplicate = new(PaymentFailureReason.DuplicateTransaction, "INS-5", "Duplicate");

    private readonly ScriptedGateway _inner = new();

    [Fact]
    public async Task Retry_recovers_from_transient_failures()
    {
        _inner.Requests.Enqueue(Unavailable);
        _inner.Requests.Enqueue(Unavailable);
        _inner.Requests.Enqueue(new GatewayAcceptance("MP1"));

        var result = await Retrying().RequestPaymentAsync(Request(), CancellationToken.None);

        result.Value.OperatorTransactionId.ShouldBe("MP1");
        _inner.RequestCalls.ShouldBe(3);
    }

    [Fact]
    public async Task Retry_gives_up_after_the_configured_attempts()
    {
        for (var i = 0; i < 5; i++)
        {
            _inner.Requests.Enqueue(Unavailable);
        }

        var result = await Retrying(maxRetries: 2).RequestPaymentAsync(Request(), CancellationToken.None);

        result.Error.ShouldBe(Unavailable);
        _inner.RequestCalls.ShouldBe(3);
    }

    [Fact]
    public async Task Retry_never_repeats_a_business_answer()
    {
        _inner.Requests.Enqueue(NoBalance);

        var result = await Retrying().RequestPaymentAsync(Request(), CancellationToken.None);

        result.Error.ShouldBe(NoBalance);
        _inner.RequestCalls.ShouldBe(1);
    }

    [Fact]
    public async Task A_duplicate_after_a_retry_recovers_the_transaction_the_first_attempt_created()
    {
        _inner.Requests.Enqueue(Unavailable); // the request did reach the operator; only the answer was lost
        _inner.Requests.Enqueue(Duplicate);
        _inner.Statuses.Enqueue(new GatewayPaymentStatus(Guid.Empty, GatewayPaymentState.Pending, "MP-FIRST"));

        var result = await Retrying().RequestPaymentAsync(Request(), CancellationToken.None);

        result.Value.OperatorTransactionId.ShouldBe("MP-FIRST");
        _inner.StatusCalls.ShouldBe(1);
    }

    [Fact]
    public async Task A_duplicate_on_the_first_attempt_is_a_real_duplicate()
    {
        _inner.Requests.Enqueue(Duplicate);

        var result = await Retrying().RequestPaymentAsync(Request(), CancellationToken.None);

        result.Error.ShouldBe(Duplicate);
        _inner.StatusCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Retry_also_covers_status_queries()
    {
        _inner.Statuses.Enqueue(Unavailable);
        _inner.Statuses.Enqueue(new GatewayPaymentStatus(Guid.Empty, GatewayPaymentState.Confirmed, "MP1"));

        var status = await Retrying().QueryStatusAsync(Guid.NewGuid(), CancellationToken.None);

        status.Value.State.ShouldBe(GatewayPaymentState.Confirmed);
        _inner.StatusCalls.ShouldBe(2);
    }

    [Fact]
    public async Task Caching_keeps_final_statuses_only()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var caching = new CachingGatewayDecorator(_inner, cache);
        var pending = Guid.NewGuid();
        var confirmed = Guid.NewGuid();
        _inner.Statuses.Enqueue(new GatewayPaymentStatus(pending, GatewayPaymentState.Pending, "MP1"));
        _inner.Statuses.Enqueue(new GatewayPaymentStatus(pending, GatewayPaymentState.Pending, "MP1"));
        _inner.Statuses.Enqueue(new GatewayPaymentStatus(confirmed, GatewayPaymentState.Confirmed, "MP2"));

        await caching.QueryStatusAsync(pending, CancellationToken.None);
        await caching.QueryStatusAsync(pending, CancellationToken.None);
        var first = await caching.QueryStatusAsync(confirmed, CancellationToken.None);
        var second = await caching.QueryStatusAsync(confirmed, CancellationToken.None);

        _inner.StatusCalls.ShouldBe(3);
        second.Value.ShouldBe(first.Value);
    }

    [Fact]
    public async Task Metrics_count_calls_by_operator_operation_and_outcome()
    {
        using var metrics = new GatewayMetrics();
        var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == GatewayMetrics.MeterName && instrument.Name == "kanimambo.payments.gateway.calls")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            measurements.Add((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
        listener.Start();
        _inner.Requests.Enqueue(NoBalance);

        await new MetricsGatewayDecorator(_inner, metrics).RequestPaymentAsync(Request(), CancellationToken.None);

        var (value, tags) = measurements.ShouldHaveSingleItem();
        value.ShouldBe(1);
        tags["operator"].ShouldBe("MPesa");
        tags["operation"].ShouldBe("request_payment");
        tags["outcome"].ShouldBe("payments.insufficient_balance");
    }

    [Fact]
    public async Task Logging_records_each_call_and_passes_the_result_through_untouched()
    {
        var logger = new ListLogger<LoggingGatewayDecorator>();
        var request = Request();
        var acceptance = new GatewayAcceptance("MP1");
        _inner.Requests.Enqueue(acceptance);

        var result = await new LoggingGatewayDecorator(_inner, logger).RequestPaymentAsync(request, CancellationToken.None);

        result.Value.ShouldBeSameAs(acceptance);
        logger.Messages.ShouldHaveSingleItem().ShouldContain($"MPesa RequestPayment for payment {request.PaymentId}: ok");
    }

    [Fact]
    public void Decorators_preserve_the_operator_and_forward_callbacks()
    {
        var chain = new LoggingGatewayDecorator(
            new RetryingGatewayDecorator(_inner, 2, TimeSpan.Zero), new ListLogger<LoggingGatewayDecorator>());

        chain.Operator.ShouldBe(MobileOperator.MPesa);
        chain.ParseCallback("{}", "sig").Value.OperatorTransactionId.ShouldBe("from-callback");
    }

    private RetryingGatewayDecorator Retrying(int maxRetries = 2) => new(_inner, maxRetries, TimeSpan.FromMilliseconds(1));

    private static MobileMoneyRequest Request() =>
        new(Guid.NewGuid(), "258841234567", Money.Mzn(100m), new Uri("https://api.test/cb"), "test");

    private sealed class ScriptedGateway : IMobileMoneyGateway
    {
        public Queue<Result<GatewayAcceptance>> Requests { get; } = new();

        public Queue<Result<GatewayPaymentStatus>> Statuses { get; } = new();

        public int RequestCalls { get; private set; }

        public int StatusCalls { get; private set; }

        public MobileOperator Operator => MobileOperator.MPesa;

        public Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken)
        {
            RequestCalls++;
            return Task.FromResult(Requests.Dequeue());
        }

        public Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            StatusCalls++;
            return Task.FromResult(Statuses.Dequeue());
        }

        public Result<GatewayPaymentStatus> ParseCallback(string body, string? signature) =>
            new GatewayPaymentStatus(Guid.Empty, GatewayPaymentState.Confirmed, "from-callback");
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
