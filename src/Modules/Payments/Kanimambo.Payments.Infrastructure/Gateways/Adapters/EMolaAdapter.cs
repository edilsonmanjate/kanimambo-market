using System.Net.Http.Json;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Infrastructure.Gateways.Adapters;

/// <summary>Adapter for the simulated e-Mola API: <c>status</c> words, textual <c>errorCode</c>s, amounts in cents.</summary>
internal sealed class EMolaAdapter(HttpClient http, OperatorOptions options) : IMobileMoneyGateway
{
    private static readonly Dictionary<string, PaymentFailureReason> Codes = new(StringComparer.Ordinal)
    {
        ["BALANCE_LOW"] = PaymentFailureReason.InsufficientBalance,
        ["DUPLICATE_REFERENCE"] = PaymentFailureReason.DuplicateTransaction,
        ["INVALID_MSISDN"] = PaymentFailureReason.InvalidNumber,
        ["TIMEOUT"] = PaymentFailureReason.Timeout,
    };

    public MobileOperator Operator => MobileOperator.EMola;

    public async Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await GatewayHttp.SendAsync<EMolaPayment>(() => http.PostAsJsonAsync("emola/payments", new EMolaRequest(
            request.PaymentId, request.Phone, (long)(request.Amount.Amount * 100m), request.CallbackUrl.ToString()), GatewayHttp.Json, cancellationToken), cancellationToken);

        if (response.IsFailure)
        {
            return response.Error;
        }

        return response.Value.Status == "PENDING"
            ? new GatewayAcceptance(response.Value.TransactionId ?? string.Empty)
            : new GatewayError(ReasonOf(response.Value.ErrorCode), response.Value.ErrorCode, $"e-Mola: {response.Value.ErrorCode}");
    }

    public async Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var response = await GatewayHttp.SendAsync<EMolaPayment>(() => http.GetAsync(new Uri($"emola/payments/{paymentId}", UriKind.Relative), cancellationToken), cancellationToken);
        return response.IsFailure ? response.Error : ToStatus(paymentId, response.Value);
    }

    public Result<GatewayPaymentStatus> ParseCallback(string body, string? signature)
    {
        var callback = GatewayHttp.ParseSigned<EMolaPayment>(body, signature, options.CallbackSecret);
        return callback.IsFailure ? callback.Error : ToStatus(callback.Value.Reference, callback.Value);
    }

    private static GatewayPaymentStatus ToStatus(Guid paymentId, EMolaPayment payment) => payment.Status switch
    {
        "SUCCESS" => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Confirmed, payment.TransactionId ?? string.Empty),
        "FAILED" or "REJECTED" => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Failed, payment.TransactionId ?? string.Empty, ReasonOf(payment.ErrorCode), payment.ErrorCode),
        _ => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Pending, payment.TransactionId ?? string.Empty),
    };

    private static PaymentFailureReason ReasonOf(string? code) =>
        code is not null && Codes.TryGetValue(code, out var reason) ? reason : PaymentFailureReason.Rejected;

    private sealed record EMolaRequest(Guid Reference, string Msisdn, long AmountCents, string CallbackUrl);

    private sealed record EMolaPayment(Guid Reference, string Status, string? ErrorCode, string? TransactionId);
}
