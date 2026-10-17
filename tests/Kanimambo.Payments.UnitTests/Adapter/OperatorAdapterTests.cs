using System.Net;
using System.Text;
using System.Text.Json;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.Payments.Infrastructure.Gateways.Adapters;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.UnitTests.Adapter;

/// <summary>Each adapter translates its operator's (simulated) wire format into the one <see cref="IMobileMoneyGateway"/> vocabulary.</summary>
public sealed class OperatorAdapterTests : IDisposable
{
    private const string Secret = "unit-test-secret";
    private static readonly Uri Callback = new("https://api.test/api/payments/callbacks/x");

    private readonly StubOperator _operator = new();

    public static TheoryData<string, string, PaymentFailureReason, string> Refusals() => new()
    {
        { "MPesa", """{"output_ResponseCode":"INS-6","output_ResponseDesc":"Saldo insuficiente"}""", PaymentFailureReason.InsufficientBalance, "INS-6" },
        { "MPesa", """{"output_ResponseCode":"INS-5","output_ResponseDesc":"Duplicate"}""", PaymentFailureReason.DuplicateTransaction, "INS-5" },
        { "MPesa", """{"output_ResponseCode":"INS-10","output_ResponseDesc":"Invalid"}""", PaymentFailureReason.InvalidNumber, "INS-10" },
        { "MPesa", """{"output_ResponseCode":"INS-2051","output_ResponseDesc":"Unknown"}""", PaymentFailureReason.Rejected, "INS-2051" },
        { "EMola", """{"reference":"00000000-0000-0000-0000-000000000000","status":"REJECTED","errorCode":"BALANCE_LOW"}""", PaymentFailureReason.InsufficientBalance, "BALANCE_LOW" },
        { "EMola", """{"reference":"00000000-0000-0000-0000-000000000000","status":"REJECTED","errorCode":"DUPLICATE_REFERENCE"}""", PaymentFailureReason.DuplicateTransaction, "DUPLICATE_REFERENCE" },
        { "EMola", """{"reference":"00000000-0000-0000-0000-000000000000","status":"REJECTED","errorCode":"INVALID_MSISDN"}""", PaymentFailureReason.InvalidNumber, "INVALID_MSISDN" },
        { "MKesh", """{"codigo":51,"mensagem":"Saldo insuficiente"}""", PaymentFailureReason.InsufficientBalance, "51" },
        { "MKesh", """{"codigo":94,"mensagem":"Transacção duplicada"}""", PaymentFailureReason.DuplicateTransaction, "94" },
        { "MKesh", """{"codigo":14,"mensagem":"Número inválido"}""", PaymentFailureReason.InvalidNumber, "14" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Native_refusal_codes_become_the_shared_failure_reasons(string op, string body, PaymentFailureReason reason, string nativeCode)
    {
        _operator.Reply(body);

        var result = await Adapter(op).RequestPaymentAsync(Request(), CancellationToken.None);

        var error = result.Error.ShouldBeOfType<GatewayError>();
        error.Reason.ShouldBe(reason);
        error.NativeCode.ShouldBe(nativeCode);
        error.IsTransient.ShouldBeFalse();
    }

    [Theory]
    [InlineData("MPesa", """{"output_ResponseCode":"INS-0","output_TransactionID":"MP42"}""", "MP42")]
    [InlineData("EMola", """{"reference":"00000000-0000-0000-0000-000000000000","status":"PENDING","transactionId":"EM-42"}""", "EM-42")]
    [InlineData("MKesh", """{"codigo":0,"mensagem":"Cobrança aceite","idTransacao":"MK42"}""", "MK42")]
    public async Task An_accepted_request_returns_the_operators_transaction_id(string op, string body, string transactionId)
    {
        _operator.Reply(body);

        var result = await Adapter(op).RequestPaymentAsync(Request(), CancellationToken.None);

        result.Value.OperatorTransactionId.ShouldBe(transactionId);
    }

    [Theory]
    [InlineData("MPesa", "mpesa/c2b", "\"input_Amount\":\"1234.50\"")]
    [InlineData("EMola", "emola/payments", "\"amountCents\":123450")]
    [InlineData("MKesh", "mkesh/v1/cobrancas", "\"valor\":\"1234,50\"")]
    public async Task Each_operator_gets_the_amount_in_its_own_format(string op, string path, string amountFragment)
    {
        _operator.Reply("""{"output_ResponseCode":"INS-0","status":"PENDING","codigo":0,"reference":"00000000-0000-0000-0000-000000000000"}""");

        await Adapter(op).RequestPaymentAsync(Request(), CancellationToken.None);

        _operator.LastPath.ShouldBe("/" + path);
        _operator.LastBody.ShouldContain(amountFragment);
    }

    [Theory]
    [InlineData("MPesa")]
    [InlineData("EMola")]
    [InlineData("MKesh")]
    public async Task Server_errors_and_unreachable_operators_are_transient(string op)
    {
        _operator.Reply("oops", HttpStatusCode.ServiceUnavailable);
        var unavailable = await Adapter(op).RequestPaymentAsync(Request(), CancellationToken.None);

        _operator.Throw(new HttpRequestException("connection refused"));
        var unreachable = await Adapter(op).QueryStatusAsync(Guid.NewGuid(), CancellationToken.None);

        unavailable.Error.ShouldBeOfType<GatewayError>().ShouldSatisfyAllConditions(
            e => e.Reason.ShouldBe(PaymentFailureReason.GatewayUnavailable),
            e => e.IsTransient.ShouldBeTrue(),
            e => e.Type.ShouldBe(ErrorType.Unavailable));
        unreachable.Error.ShouldBeOfType<GatewayError>().IsTransient.ShouldBeTrue();
    }

    [Theory]
    [InlineData("MPesa", """{"output_ResponseCode":"INS-0","output_ResponseTransactionStatus":"Completed","output_TransactionID":"MP1"}""", GatewayPaymentState.Confirmed)]
    [InlineData("MPesa", """{"output_ResponseCode":"INS-0","output_ResponseTransactionStatus":"Pending","output_TransactionID":"MP1"}""", GatewayPaymentState.Pending)]
    [InlineData("EMola", """{"reference":"00000000-0000-0000-0000-000000000000","status":"FAILED","errorCode":"BALANCE_LOW"}""", GatewayPaymentState.Failed)]
    [InlineData("MKesh", """{"estado":"CONCLUIDA","codigo":0,"idTransacao":"MK1"}""", GatewayPaymentState.Confirmed)]
    [InlineData("MKesh", """{"estado":"PENDENTE","codigo":0}""", GatewayPaymentState.Pending)]
    public async Task Status_queries_are_translated(string op, string body, GatewayPaymentState expected)
    {
        _operator.Reply(body);
        var paymentId = Guid.NewGuid();

        var status = await Adapter(op).QueryStatusAsync(paymentId, CancellationToken.None);

        status.Value.State.ShouldBe(expected);
        status.Value.PaymentId.ShouldBe(paymentId);
        _operator.LastPath.ShouldEndWith(paymentId.ToString());
    }

    public static TheoryData<string, string, GatewayPaymentState, PaymentFailureReason?> Callbacks()
    {
        var id = PaymentId;
        return new()
        {
            { "MPesa", JsonSerializer.Serialize(new Dictionary<string, string> { ["output_ResponseCode"] = "INS-0", ["output_TransactionID"] = "MP7", ["output_ThirdPartyReference"] = id.ToString() }), GatewayPaymentState.Confirmed, null },
            { "MPesa", JsonSerializer.Serialize(new Dictionary<string, string> { ["output_ResponseCode"] = "INS-6", ["output_TransactionID"] = "MP7", ["output_ThirdPartyReference"] = id.ToString() }), GatewayPaymentState.Failed, PaymentFailureReason.InsufficientBalance },
            { "EMola", $$"""{"reference":"{{id}}","status":"SUCCESS","transactionId":"EM7"}""", GatewayPaymentState.Confirmed, null },
            { "EMola", $$"""{"reference":"{{id}}","status":"FAILED","errorCode":"BALANCE_LOW"}""", GatewayPaymentState.Failed, PaymentFailureReason.InsufficientBalance },
            { "MKesh", $$"""{"ref":"{{id}}","idTransacao":"MK7","codigo":0}""", GatewayPaymentState.Confirmed, null },
            { "MKesh", $$"""{"ref":"{{id}}","idTransacao":"MK7","codigo":51}""", GatewayPaymentState.Failed, PaymentFailureReason.InsufficientBalance },
        };
    }

    private static readonly Guid PaymentId = Guid.Parse("6f1c1f7e-2a9b-4c55-9d1e-7f3a0b2c4d5e");

    [Theory]
    [MemberData(nameof(Callbacks))]
    public void Signed_callbacks_are_translated(string op, string body, GatewayPaymentState state, PaymentFailureReason? reason)
    {
        var status = Adapter(op).ParseCallback(body, CallbackSignature.Sign(body, Secret));

        status.Value.PaymentId.ShouldBe(PaymentId);
        status.Value.State.ShouldBe(state);
        status.Value.FailureReason.ShouldBe(reason);
    }

    [Theory]
    [InlineData("MPesa")]
    [InlineData("EMola")]
    [InlineData("MKesh")]
    public void Callbacks_with_a_missing_or_wrong_signature_are_rejected(string op)
    {
        var body = $$"""{"reference":"{{PaymentId}}","ref":"{{PaymentId}}","status":"SUCCESS","codigo":0}""";
        var adapter = Adapter(op);

        var unsigned = adapter.ParseCallback(body, null);
        var forged = adapter.ParseCallback(body, CallbackSignature.Sign(body, "someone-elses-secret"));
        var tampered = adapter.ParseCallback(body.Replace("SUCCESS", "FAILED", StringComparison.Ordinal), CallbackSignature.Sign(body, Secret));

        foreach (var result in new[] { unsigned, forged, tampered })
        {
            result.Error.Code.ShouldBe("payments.invalid_signature");
            result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        }
    }

    public void Dispose() => _operator.Dispose();

    private IMobileMoneyGateway Adapter(string op)
    {
        var http = new HttpClient(_operator) { BaseAddress = new Uri("http://operator.test/") };
        var options = new OperatorOptions { CallbackSecret = Secret };
        return op switch
        {
            "MPesa" => new MPesaAdapter(http, options),
            "EMola" => new EMolaAdapter(http, options),
            "MKesh" => new MKeshAdapter(http, options),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
    }

    private static MobileMoneyRequest Request() => new(Guid.NewGuid(), "258841234567", Money.Mzn(1_234.50m), Callback, "Kanimambo encomenda KM-1001");

    private sealed class StubOperator : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _reply = () => new HttpResponseMessage(HttpStatusCode.NotFound);

        public string? LastPath { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        public void Reply(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            _reply = () => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        public void Throw(Exception exception) => _reply = () => throw exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return _reply();
        }
    }
}
