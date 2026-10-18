using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Infrastructure.Gateways.Adapters;

// The three wire formats below are the ones implemented by the local simulator (Kanimambo.Simulators.MobileMoney).
// They are deliberately different from each other to show what the adapters absorb, and they are NOT the
// official APIs of Vodacom, Movitel or Tmcel. A real adapter requires the operator's sandbox credentials.

/// <summary>Adapter for the simulated M-Pesa API: <c>input_*/output_*</c> fields and <c>INS-*</c> response codes.</summary>
internal sealed class MPesaAdapter(HttpClient http, OperatorOptions options) : IMobileMoneyGateway
{
    private static readonly Dictionary<string, PaymentFailureReason> Codes = new(StringComparer.Ordinal)
    {
        ["INS-5"] = PaymentFailureReason.DuplicateTransaction,
        ["INS-6"] = PaymentFailureReason.InsufficientBalance,
        ["INS-9"] = PaymentFailureReason.Timeout,
        ["INS-10"] = PaymentFailureReason.InvalidNumber,
    };

    public MobileOperator Operator => MobileOperator.MPesa;

    public async Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await GatewayHttp.SendAsync<MPesaResponse>(() => http.PostAsJsonAsync("mpesa/c2b", new MPesaRequest(
            request.PaymentId.ToString("N")[..12].ToUpperInvariant(),
            request.Phone,
            request.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            request.PaymentId.ToString(),
            request.CallbackUrl.ToString()), cancellationToken), cancellationToken);

        if (response.IsFailure)
        {
            return response.Error;
        }

        return response.Value.ResponseCode == "INS-0"
            ? new GatewayAcceptance(response.Value.TransactionId ?? string.Empty)
            : Failure(response.Value.ResponseCode, response.Value.ResponseDescription);
    }

    public async Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var response = await GatewayHttp.SendAsync<MPesaStatus>(() => http.GetAsync(new Uri($"mpesa/status/{paymentId}", UriKind.Relative), cancellationToken), cancellationToken);
        return response.IsFailure ? response.Error : ToStatus(paymentId, response.Value.TransactionStatus, response.Value.TransactionId, response.Value.ResponseCode);
    }

    public Result<GatewayPaymentStatus> ParseCallback(string body, string? signature)
    {
        var callback = GatewayHttp.ParseSigned<MPesaCallback>(body, signature, options.CallbackSecret);
        if (callback.IsFailure)
        {
            return callback.Error;
        }

        if (!Guid.TryParse(callback.Value.ThirdPartyReference, out var paymentId))
        {
            return Error.Validation(new FieldError("output_ThirdPartyReference", "Not one of our references."));
        }

        return ToStatus(paymentId, callback.Value.ResponseCode == "INS-0" ? "Completed" : "Failed", callback.Value.TransactionId, callback.Value.ResponseCode);
    }

    private static GatewayPaymentStatus ToStatus(Guid paymentId, string? transactionStatus, string? transactionId, string? code) => transactionStatus switch
    {
        "Completed" => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Confirmed, transactionId ?? string.Empty),
        "Failed" => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Failed, transactionId ?? string.Empty, ReasonOf(code), code),
        _ => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Pending, transactionId ?? string.Empty),
    };

    private static PaymentFailureReason ReasonOf(string? code) =>
        code is not null && Codes.TryGetValue(code, out var reason) ? reason : PaymentFailureReason.Rejected;

    private static GatewayError Failure(string? code, string? description) =>
        new(ReasonOf(code), code, $"M-Pesa: {description ?? code}");

    private sealed record MPesaRequest(
        [property: JsonPropertyName("input_TransactionReference")] string TransactionReference,
        [property: JsonPropertyName("input_CustomerMSISDN")] string CustomerMsisdn,
        [property: JsonPropertyName("input_Amount")] string Amount,
        [property: JsonPropertyName("input_ThirdPartyReference")] string ThirdPartyReference,
        [property: JsonPropertyName("input_CallbackUrl")] string CallbackUrl);

    private sealed record MPesaResponse(
        [property: JsonPropertyName("output_ResponseCode")] string ResponseCode,
        [property: JsonPropertyName("output_ResponseDesc")] string? ResponseDescription,
        [property: JsonPropertyName("output_TransactionID")] string? TransactionId);

    private sealed record MPesaStatus(
        [property: JsonPropertyName("output_ResponseCode")] string? ResponseCode,
        [property: JsonPropertyName("output_ResponseTransactionStatus")] string? TransactionStatus,
        [property: JsonPropertyName("output_TransactionID")] string? TransactionId);

    private sealed record MPesaCallback(
        [property: JsonPropertyName("output_ResponseCode")] string ResponseCode,
        [property: JsonPropertyName("output_TransactionID")] string? TransactionId,
        [property: JsonPropertyName("output_ThirdPartyReference")] string ThirdPartyReference);
}
