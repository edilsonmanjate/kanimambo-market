using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kanimambo.Payments.Domain;

namespace Kanimambo.Payments.Infrastructure.Gateways.Decorators;

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
