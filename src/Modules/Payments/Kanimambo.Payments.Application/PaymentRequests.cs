using Kanimambo.Ordering.Contracts;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Application.Processing;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Payments.Application;

public static class PaymentsModule
{
    public const string Name = "Payments";
}

public interface IPaymentRepository
{
    void Add(Payment payment);

    Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Payment?> FindByOrderAttemptAsync(Guid orderId, int attempt, CancellationToken cancellationToken);

    Task<IReadOnlyList<Payment>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Mobile-money payments still waiting for the customer's PIN after their deadline.</summary>
    Task<IReadOnlyList<Payment>> ListExpiredAwaitingPinAsync(DateTimeOffset now, int max, CancellationToken cancellationToken);
}

public sealed record PaymentTransitionDto(string From, string To, DateTimeOffset At, string? Detail);

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    long OrderNumber,
    int Attempt,
    string Method,
    string? Operator,
    string Phone,
    decimal Amount,
    string Status,
    string? OperatorTransactionId,
    string? FailureReason,
    string? NativeCode,
    IReadOnlyList<PaymentTransitionDto> History)
{
    public static PaymentDto From(Payment p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new PaymentDto(p.Id, p.OrderId, p.OrderNumber, p.Attempt, p.Method.ToString(), p.Operator?.ToString(), p.Phone, p.Amount.Amount,
            p.Status.ToString(), p.OperatorTransactionId, p.FailureReason?.ToString(), p.NativeCode,
            [.. p.History.Select(h => new PaymentTransitionDto(h.From.ToString(), h.To.ToString(), h.At, h.Detail))]);
    }
}

/// <summary>Start collecting an order's payment. Idempotent: one payment per (order, attempt).</summary>
public sealed record StartPayment(Guid IdempotencyKey, Guid OrderId, long OrderNumber, int Attempt, string Method, string Phone, decimal AmountMzn)
    : IIdempotentCommand;

/// <summary>An operator's asynchronous answer (the customer typed the PIN, or it failed).</summary>
public sealed record HandleGatewayCallback(string Operator, string Body, string? Signature) : ICommand;

/// <summary>Resolves payments whose PIN prompt expired: ask the operator one last time, otherwise time them out.</summary>
public sealed record ExpirePendingPayments(int Max = 100) : ICommand<int>;

public sealed record GetPayment(Guid PaymentId) : IQuery<PaymentDto>;

public sealed record ListPaymentsForOrder(Guid OrderId) : IQuery<IReadOnlyList<PaymentDto>>;

/// <summary>Observer: Payments starts working when Ordering says an order awaits payment.</summary>
public sealed class StartPaymentWhenRequested(ISender sender) : INotificationHandler<OrderPaymentRequestedIntegrationEvent>
{
    public async ValueTask Handle(OrderPaymentRequestedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var started = await sender.Send(new StartPayment(notification.EventId, notification.OrderId, notification.OrderNumber,
            notification.Attempt, notification.PaymentMethod, notification.CustomerPhone, notification.AmountMzn), cancellationToken);

        if (started.IsFailure)
        {
            throw new InvalidOperationException($"Could not start the payment of order {notification.OrderNumber}: {started.Error.Message}");
        }
    }
}

public sealed partial class PaymentHandlers(
    IPaymentRepository payments,
    PaymentProcessorFactory processors,
    IMobileMoneyGatewayProvider gateways,
    TimeProvider timeProvider,
    ILogger<PaymentHandlers> logger) :
    IRequestHandler<StartPayment, Result>,
    IRequestHandler<HandleGatewayCallback, Result>,
    IRequestHandler<ExpirePendingPayments, Result<int>>,
    IRequestHandler<GetPayment, Result<PaymentDto>>,
    IRequestHandler<ListPaymentsForOrder, Result<IReadOnlyList<PaymentDto>>>
{
    public async ValueTask<Result> Handle(StartPayment request, CancellationToken cancellationToken)
    {
        if (await payments.FindByOrderAttemptAsync(request.OrderId, request.Attempt, cancellationToken) is not null)
        {
            return Result.Success();
        }

        if (!Enum.TryParse<PaymentMethod>(request.Method, out var method))
        {
            return Error.Validation(new FieldError("method", $"Unknown payment method '{request.Method}'."));
        }

        var now = timeProvider.GetUtcNow();
        var payment = Payment.Start(request.OrderId, request.OrderNumber, request.Attempt, method, request.Phone, Money.Mzn(request.AmountMzn), now);
        payments.Add(payment);

        var processor = processors.CreateFor(method, request.Phone);
        if (processor.IsFailure)
        {
            var error = (GatewayError)processor.Error;
            payment.Fail(error.Reason, error.NativeCode, error.Message, now);
            return Result.Success();
        }

        await processor.Value.StartAsync(payment, cancellationToken);
        return Result.Success();
    }

    public async ValueTask<Result> Handle(HandleGatewayCallback request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MobileOperator>(request.Operator, ignoreCase: true, out var mobileOperator))
        {
            return Error.NotFound("payments.unknown_operator", $"Unknown operator '{request.Operator}'.");
        }

        var callback = gateways.For(mobileOperator).ParseCallback(request.Body, request.Signature);
        if (callback.IsFailure)
        {
            return callback.Error;
        }

        var payment = await payments.GetAsync(callback.Value.PaymentId, cancellationToken);
        if (payment is null)
        {
            return Error.NotFound("payments.not_found", $"No payment {callback.Value.PaymentId}.");
        }

        var applied = Apply(payment, callback.Value);
        if (applied.IsFailure)
        {
            // Duplicate or out-of-order callbacks are acknowledged (so the operator stops retrying) and logged.
            LogIgnoredCallback(logger, payment.Id, payment.Status, callback.Value.State);
        }

        return Result.Success();
    }

    public async ValueTask<Result<int>> Handle(ExpirePendingPayments request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var expired = await payments.ListExpiredAwaitingPinAsync(now, request.Max, cancellationToken);

        foreach (var payment in expired)
        {
            // Before giving up, ask the operator: the callback may simply have been lost.
            var status = await gateways.For(payment.Operator!.Value).QueryStatusAsync(payment.Id, cancellationToken);
            if (status.IsSuccess && status.Value.IsFinal)
            {
                Apply(payment, status.Value);
            }
            else
            {
                payment.TimeOut(now);
            }
        }

        return expired.Count;
    }

    public async ValueTask<Result<PaymentDto>> Handle(GetPayment request, CancellationToken cancellationToken)
    {
        var payment = await payments.GetAsync(request.PaymentId, cancellationToken);
        return payment is null ? Error.NotFound("payments.not_found", $"No payment {request.PaymentId}.") : PaymentDto.From(payment);
    }

    public async ValueTask<Result<IReadOnlyList<PaymentDto>>> Handle(ListPaymentsForOrder request, CancellationToken cancellationToken) =>
        (await payments.ListByOrderAsync(request.OrderId, cancellationToken)).Select(PaymentDto.From).ToList();

    private Result Apply(Payment payment, GatewayPaymentStatus status)
    {
        var now = timeProvider.GetUtcNow();
        return status.State switch
        {
            GatewayPaymentState.Confirmed => payment.Confirm(status.OperatorTransactionId, now),
            GatewayPaymentState.Failed => payment.Fail(status.FailureReason ?? PaymentFailureReason.Rejected, status.NativeCode,
                $"The operator reported {status.FailureReason ?? PaymentFailureReason.Rejected} ({status.NativeCode}).", now),
            _ => Result.Success(),
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignored callback for payment {PaymentId} in state {Status}: {CallbackState}")]
    private static partial void LogIgnoredCallback(ILogger logger, Guid paymentId, PaymentStatus status, GatewayPaymentState callbackState);
}
