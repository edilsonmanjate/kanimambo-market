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

/// <summary>Adapter for the simulated mKesh API: Portuguese field names, numeric <c>codigo</c>s, decimal comma amounts.</summary>
internal sealed class MKeshAdapter(HttpClient http, OperatorOptions options) : IMobileMoneyGateway
{
    private static readonly Dictionary<int, PaymentFailureReason> Codes = new()
    {
        [51] = PaymentFailureReason.InsufficientBalance,
        [94] = PaymentFailureReason.DuplicateTransaction,
        [14] = PaymentFailureReason.InvalidNumber,
        [68] = PaymentFailureReason.Timeout,
    };

    public MobileOperator Operator => MobileOperator.MKesh;

    public async Task<Result<GatewayAcceptance>> RequestPaymentAsync(MobileMoneyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var amount = request.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
        var response = await GatewayHttp.SendAsync<MKeshResponse>(() => http.PostAsJsonAsync("mkesh/v1/cobrancas", new MKeshRequest(
            request.PaymentId, request.Phone, amount, request.CallbackUrl.ToString()), GatewayHttp.Json, cancellationToken), cancellationToken);

        if (response.IsFailure)
        {
            return response.Error;
        }

        return response.Value.Codigo == 0
            ? new GatewayAcceptance(response.Value.IdTransacao ?? string.Empty)
            : new GatewayError(ReasonOf(response.Value.Codigo), response.Value.Codigo.ToString(CultureInfo.InvariantCulture), $"mKesh: {response.Value.Mensagem}");
    }

    public async Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var response = await GatewayHttp.SendAsync<MKeshStatus>(() => http.GetAsync(new Uri($"mkesh/v1/cobrancas/{paymentId}", UriKind.Relative), cancellationToken), cancellationToken);
        if (response.IsFailure)
        {
            return response.Error;
        }

        var status = response.Value;
        return status.Estado switch
        {
            "CONCLUIDA" => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Confirmed, status.IdTransacao ?? string.Empty),
            "FALHADA" => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Failed, status.IdTransacao ?? string.Empty, ReasonOf(status.Codigo), status.Codigo.ToString(CultureInfo.InvariantCulture)),
            _ => new GatewayPaymentStatus(paymentId, GatewayPaymentState.Pending, status.IdTransacao ?? string.Empty),
        };
    }

    public Result<GatewayPaymentStatus> ParseCallback(string body, string? signature)
    {
        var callback = GatewayHttp.ParseSigned<MKeshCallback>(body, signature, options.CallbackSecret);
        if (callback.IsFailure)
        {
            return callback.Error;
        }

        var c = callback.Value;
        return c.Codigo == 0
            ? new GatewayPaymentStatus(c.Ref, GatewayPaymentState.Confirmed, c.IdTransacao ?? string.Empty)
            : new GatewayPaymentStatus(c.Ref, GatewayPaymentState.Failed, c.IdTransacao ?? string.Empty, ReasonOf(c.Codigo), c.Codigo.ToString(CultureInfo.InvariantCulture));
    }

    private static PaymentFailureReason ReasonOf(int code) => Codes.GetValueOrDefault(code, PaymentFailureReason.Rejected);

    private sealed record MKeshRequest(Guid Ref, string Telefone, string Valor, string UrlRetorno);

    private sealed record MKeshResponse(int Codigo, string? Mensagem, string? IdTransacao);

    private sealed record MKeshStatus(string Estado, int Codigo, string? IdTransacao);

    private sealed record MKeshCallback(Guid Ref, string? IdTransacao, int Codigo);
}
