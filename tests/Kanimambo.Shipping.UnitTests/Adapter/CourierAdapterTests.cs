using System.Net;
using System.Text;
using System.Text.Json;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Infrastructure.Webhooks;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Application.Couriers;
using Kanimambo.Shipping.Domain;
using Kanimambo.Shipping.Infrastructure.Couriers;

namespace Kanimambo.Shipping.UnitTests.Adapter;

/// <summary>Each courier adapter translates its (simulated) courier's format into the one <see cref="ICourierGateway"/> vocabulary.</summary>
public sealed class CourierAdapterTests : IDisposable
{
    private const string Secret = "unit-test-secret";

    private readonly StubCourier _courier = new();

    public void Dispose() => _courier.Dispose();

    [Theory]
    [InlineData("Motorbike", "\"vehicle\":\"MOTO\"")]
    [InlineData("Txopela", "\"vehicle\":\"TXOPELA\"")]
    public async Task Txova_gets_kilograms_and_its_own_vehicle_codes(string vehicle, string fragment)
    {
        _courier.Reply("""{"deliveryId":"TX-1","reference":"KM-1-1","status":"ASSIGNED"}""", HttpStatusCode.Created);

        var booking = await Adapter("Txova").BookAsync(Request(vehicle), CancellationToken.None);

        booking.Value.TrackingCode.ShouldBe("TX-1");
        _courier.LastPath.ShouldBe("/txova/v1/deliveries");
        _courier.LastBody.ShouldContain(fragment);
        _courier.LastBody.ShouldContain("\"weightKg\":12.25");
        _courier.LastBody.ShouldContain("\"cashToCollect\":1234.5");
    }

    [Fact]
    public async Task Mbila_gets_grams_and_a_decimal_comma_amount()
    {
        _courier.Reply("""{"codigo":"00","mensagem":"Guia emitida","numeroGuia":"MB77"}""");

        var booking = await Adapter("Mbila").BookAsync(Request(null), CancellationToken.None);

        booking.Value.TrackingCode.ShouldBe("MB77");
        _courier.LastPath.ShouldBe("/mbila/api/guias");
        _courier.LastBody.ShouldContain("\"pesoGramas\":12250");
        _courier.LastBody.ShouldContain("\"valorCobranca\":\"1234,50\"");
        _courier.LastBody.ShouldContain("\"referenciaCliente\":\"KM-1-1\"");
    }

    public static TheoryData<string, string, HttpStatusCode, CourierFailureReason, string> Refusals() => new()
    {
        { "Txova", """{"error":"OUT_OF_AREA","message":"No"}""", HttpStatusCode.UnprocessableEntity, CourierFailureReason.DestinationNotServed, "OUT_OF_AREA" },
        { "Txova", """{"error":"TOO_HEAVY","message":"No"}""", HttpStatusCode.UnprocessableEntity, CourierFailureReason.Overweight, "TOO_HEAVY" },
        { "Mbila", """{"codigo":"E12","mensagem":"Destino não servido"}""", HttpStatusCode.OK, CourierFailureReason.DestinationNotServed, "E12" },
        { "Mbila", """{"codigo":"E30","mensagem":"Peso excede"}""", HttpStatusCode.OK, CourierFailureReason.Overweight, "E30" },
        { "Mbila", """{"codigo":"E41","mensagem":"Telefone inválido"}""", HttpStatusCode.OK, CourierFailureReason.InvalidRecipient, "E41" },
        { "Mbila", """{"codigo":"E99","mensagem":"?"}""", HttpStatusCode.OK, CourierFailureReason.Rejected, "E99" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Native_refusals_become_the_shared_reasons(string courier, string body, HttpStatusCode status, CourierFailureReason reason, string native)
    {
        _courier.Reply(body, status);

        var error = (await Adapter(courier).BookAsync(Request(null), CancellationToken.None)).Error.ShouldBeOfType<CourierError>();

        error.Reason.ShouldBe(reason);
        error.NativeCode.ShouldBe(native);
        error.IsTransient.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Txova")]
    [InlineData("Mbila")]
    public async Task Outages_are_transient(string courier)
    {
        _courier.Reply("down", HttpStatusCode.ServiceUnavailable);
        var unavailable = await Adapter(courier).BookAsync(Request(null), CancellationToken.None);

        _courier.Throw(new HttpRequestException("connection refused"));
        var unreachable = await Adapter(courier).BookAsync(Request(null), CancellationToken.None);

        unavailable.Error.ShouldBeOfType<CourierError>().IsTransient.ShouldBeTrue();
        unreachable.Error.ShouldBeOfType<CourierError>().IsTransient.ShouldBeTrue();
        unavailable.Error.Type.ShouldBe(ErrorType.Unavailable);
    }

    [Theory]
    [InlineData("Txova", """{"deliveryId":"TX-1","reference":"KM-1-1","event":"PICKED_UP"}""", ParcelStatus.PickedUp)]
    [InlineData("Txova", """{"deliveryId":"TX-1","reference":"KM-1-1","event":"DELIVERED"}""", ParcelStatus.Delivered)]
    [InlineData("Txova", """{"deliveryId":"TX-1","reference":"KM-1-1","event":"FAILED","reason":"RECIPIENT_ABSENT"}""", ParcelStatus.Failed)]
    [InlineData("Mbila", """{"numeroGuia":"MB1","referenciaCliente":"KM-1-1","estado":"RECOLHIDO"}""", ParcelStatus.PickedUp)]
    [InlineData("Mbila", """{"numeroGuia":"MB1","referenciaCliente":"KM-1-1","estado":"EM_TRANSITO"}""", ParcelStatus.InTransit)]
    [InlineData("Mbila", """{"numeroGuia":"MB1","referenciaCliente":"KM-1-1","estado":"ENTREGUE"}""", ParcelStatus.Delivered)]
    [InlineData("Mbila", """{"numeroGuia":"MB1","referenciaCliente":"KM-1-1","estado":"DEVOLVIDO","observacao":"Destinatário ausente"}""", ParcelStatus.Failed)]
    public void Signed_webhooks_are_translated(string courier, string body, ParcelStatus status)
    {
        var update = Adapter(courier).ParseWebhook(body, WebhookSignature.Sign(body, Secret)).Value;

        update.Reference.ShouldBe("KM-1-1");
        update.Status.ShouldBe(status);
        if (status == ParcelStatus.Failed)
        {
            update.Detail.ShouldNotBeNull().ShouldStartWith(courier);
        }
    }

    [Theory]
    [InlineData("Txova")]
    [InlineData("Mbila")]
    public void Webhooks_with_a_missing_or_wrong_signature_are_rejected(string courier)
    {
        const string body = """{"reference":"KM-1-1","referenciaCliente":"KM-1-1","event":"DELIVERED","estado":"ENTREGUE"}""";
        var adapter = Adapter(courier);

        foreach (var signature in new[] { null, WebhookSignature.Sign(body, "another-secret") })
        {
            adapter.ParseWebhook(body, signature).Error.Type.ShouldBe(ErrorType.Unauthorized);
        }
    }

    [Fact]
    public void Unknown_states_are_rejected_not_guessed()
    {
        var body = JsonSerializer.Serialize(new { numeroGuia = "MB1", referenciaCliente = "KM-1-1", estado = "PERDIDO" });

        Adapter("Mbila").ParseWebhook(body, WebhookSignature.Sign(body, Secret)).Error.ShouldBeOfType<ValidationError>();
    }

    private ICourierGateway Adapter(string courier)
    {
        var http = new HttpClient(_courier) { BaseAddress = new Uri("http://courier.test/") };
        var options = new CourierConnectionOptions { WebhookSecret = Secret };
        return courier == "Txova" ? new TxovaAdapter(http, options) : new MbilaAdapter(http, options);
    }

    private static CourierBookingRequest Request(string? vehicle) =>
        new("KM-1-1", "Maputo", "Maputo", "KaMpfumo", null, "258841234567", 12.25m, vehicle, Money.Mzn(1_234.50m), new Uri("https://api.test/hook"));

    private sealed class StubCourier : HttpMessageHandler
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
