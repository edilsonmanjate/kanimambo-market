using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Application.Processing;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Payments.Application;

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
