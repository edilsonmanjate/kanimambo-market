using System.Globalization;
using System.Net.Http.Json;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Infrastructure.Gateways.Adapters;

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
