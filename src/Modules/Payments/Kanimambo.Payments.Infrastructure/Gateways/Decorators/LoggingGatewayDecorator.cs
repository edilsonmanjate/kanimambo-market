using System.Diagnostics;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Payments.Infrastructure.Gateways.Decorators;

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
