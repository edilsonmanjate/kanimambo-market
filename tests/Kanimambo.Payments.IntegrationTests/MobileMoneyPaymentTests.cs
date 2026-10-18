using System.Net;
using System.Text;
using System.Text.Json;
using Kanimambo.SharedKernel.Infrastructure.Webhooks;
using Kanimambo.Simulators.MobileMoney;
using Microsoft.Extensions.DependencyInjection;
using static Kanimambo.Payments.IntegrationTests.Journey;

namespace Kanimambo.Payments.IntegrationTests;

/// <summary>
/// End to end: checkout → Ordering asks for payment (outbox) → Payments picks the operator (Factory Method) and calls
/// the simulator through its adapter and decorators → the simulated customer confirms → signed callback → Payments
/// publishes the outcome (outbox) → Ordering marks the order paid or failed.
/// </summary>
public sealed class MobileMoneyPaymentTests(PaymentsApiFactory factory)
{
    private readonly HttpClient _customer = factory.CreateClientAs($"cliente-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("MPesa", "84", "MP")]
    [InlineData("EMola", "87", "EM-")]
    [InlineData("MKesh", "82", "MK")]
    public async Task A_confirmed_wallet_payment_marks_the_order_paid_and_reserves_stock(string method, string prefix, string referencePrefix)
    {
        var product = await factory.CreateProductAsync(1_000m, 10);

        var orderId = await _customer.PlaceOrderAsync(product, method, Phone(prefix));

        var order = await _customer.OrderReachesAsync(orderId, "Paid");
        order.GetProperty("paymentReference").GetString().ShouldStartWith(referencePrefix);
        await EventuallyAsync(async () => await _customer.StockAsync(product) == 8, "Catalog reserved the stock");

        var payment = (await factory.Backoffice.PaymentsAsync(orderId)).ShouldHaveSingleItem();
        payment.GetProperty("operator").GetString().ShouldBe(method);
        payment.GetProperty("status").GetString().ShouldBe("Confirmed");
        payment.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("to").GetString())
            .ShouldBe(["AwaitingCustomerPin", "Confirmed"]);
    }

    [Theory]
    [InlineData("MPesa", "85", "INS-6")]
    [InlineData("EMola", "86", "BALANCE_LOW")]
    [InlineData("MKesh", "83", "51")]
    public async Task Insufficient_balance_fails_the_order_payment_and_keeps_the_operators_code(string method, string prefix, string nativeCode)
    {
        var product = await factory.CreateProductAsync(1_000m, 10);

        var orderId = await _customer.PlaceOrderAsync(product, method, Phone(prefix, "0001"));

        var order = await _customer.OrderReachesAsync(orderId, "PaymentFailed");
        order.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).ShouldContain("RetryPayment");
        var payment = (await factory.Backoffice.PaymentsAsync(orderId)).ShouldHaveSingleItem();
        payment.GetProperty("failureReason").GetString().ShouldBe("InsufficientBalance");
        payment.GetProperty("nativeCode").GetString().ShouldBe(nativeCode);
        (await _customer.StockAsync(product)).ShouldBe(10);
    }

    [Fact]
    public async Task A_duplicate_is_refused_synchronously_by_the_operator()
    {
        var product = await factory.CreateProductAsync(1_000m, 10);

        var orderId = await _customer.PlaceOrderAsync(product, "EMola", Phone("86", "0002"));

        await _customer.OrderReachesAsync(orderId, "PaymentFailed");
        var payment = (await factory.Backoffice.PaymentsAsync(orderId)).ShouldHaveSingleItem();
        payment.GetProperty("status").GetString().ShouldBe("Failed");
        payment.GetProperty("failureReason").GetString().ShouldBe("DuplicateTransaction");
        payment.GetProperty("nativeCode").GetString().ShouldBe("DUPLICATE_REFERENCE");
        payment.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("to").GetString()).ShouldBe(["Failed"]);
    }

    [Theory]
    [InlineData("MPesa", "84")]
    [InlineData("EMola", "86")]
    [InlineData("MKesh", "83")]
    public async Task An_operator_outage_is_retried_by_the_decorator_alone_then_fails_the_payment(string method, string prefix)
    {
        var product = await factory.CreateProductAsync(1_000m, 10);

        var orderId = await _customer.PlaceOrderAsync(product, method, Phone(prefix, "0006"));

        await _customer.OrderReachesAsync(orderId, "PaymentFailed");
        var payment = (await factory.Backoffice.PaymentsAsync(orderId)).ShouldHaveSingleItem();
        payment.GetProperty("failureReason").GetString().ShouldBe("GatewayUnavailable");

        // 1 call + GatewayMaxRetries (2). A generic HTTP retry layer underneath would multiply this (3 × 4 = 12).
        var ledger = factory.MobileMoney.Services.GetRequiredService<Ledger>();
        ledger.RequestsFor(payment.GetProperty("id").GetGuid()).ShouldBe(3);
    }

    [Fact]
    public async Task An_unanswered_pin_prompt_times_out()
    {
        var product = await factory.CreateProductAsync(1_000m, 10);

        var orderId = await _customer.PlaceOrderAsync(product, "MPesa", Phone("84", "0003"));

        await _customer.OrderReachesAsync(orderId, "PaymentFailed");
        var payment = (await factory.Backoffice.PaymentsAsync(orderId)).ShouldHaveSingleItem();
        payment.GetProperty("status").GetString().ShouldBe("TimedOut");
        payment.GetProperty("failureReason").GetString().ShouldBe("Timeout");
    }

    [Fact]
    public async Task A_confirmation_that_arrives_after_the_timeout_still_pays_the_order()
    {
        var product = await factory.CreateProductAsync(1_000m, 10);

        var orderId = await _customer.PlaceOrderAsync(product, "MKesh", Phone("82", "0004"));

        await _customer.OrderReachesAsync(orderId, "PaymentFailed");
        var paid = await _customer.OrderReachesAsync(orderId, "Paid");

        paid.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("to").GetString())
            .ShouldBe(["AwaitingPayment", "PaymentFailed", "Paid"]);
        var payment = (await factory.Backoffice.PaymentsAsync(orderId)).ShouldHaveSingleItem();
        payment.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("to").GetString())
            .ShouldBe(["AwaitingCustomerPin", "TimedOut", "Confirmed"]);
        await EventuallyAsync(async () => await _customer.StockAsync(product) == 8, "Catalog reserved the stock");
    }

    [Theory]
    [InlineData("MPesa", "89", "UnknownOperator")]
    [InlineData("EMola", "84", "WalletMismatch")]
    public async Task Numbers_the_chosen_wallet_cannot_serve_fail_without_calling_any_operator(string method, string prefix, string reason)
    {
        var product = await factory.CreateProductAsync(1_000m, 10);

        var orderId = await _customer.PlaceOrderAsync(product, method, Phone(prefix));

        await _customer.OrderReachesAsync(orderId, "PaymentFailed");
        var payment = (await factory.Backoffice.PaymentsAsync(orderId)).ShouldHaveSingleItem();
        payment.GetProperty("failureReason").GetString().ShouldBe(reason);
        payment.GetProperty("operator").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Card_payments_are_settled_synchronously()
    {
        var product = await factory.CreateProductAsync(1_000m, 10);

        var orderId = await _customer.PlaceOrderAsync(product, "Card", Phone("84"));

        var order = await _customer.OrderReachesAsync(orderId, "Paid");
        order.GetProperty("paymentReference").GetString().ShouldStartWith("SIMO-");
    }

    [Fact]
    public async Task The_customer_can_retry_a_failed_payment_and_each_attempt_is_a_separate_payment()
    {
        var product = await factory.CreateProductAsync(1_000m, 10);
        var orderId = await _customer.PlaceOrderAsync(product, "MPesa", Phone("84", "0001"));
        await _customer.OrderReachesAsync(orderId, "PaymentFailed");

        var retried = await _customer.PostAsync(new Uri($"/api/orders/{orderId}/retry-payment", UriKind.Relative), null, Ct).Json();

        retried.GetProperty("status").GetString().ShouldBe("AwaitingPayment");
        await EventuallyAsync(async () => (await factory.Backoffice.PaymentsAsync(orderId)).Count(p => p.GetProperty("status").GetString() == "Failed") == 2,
            "the second attempt failed too (same empty wallet)");
        var attempts = await factory.Backoffice.PaymentsAsync(orderId);
        attempts.Select(p => p.GetProperty("attempt").GetInt32()).Order().ShouldBe([1, 2]);
        await _customer.OrderReachesAsync(orderId, "PaymentFailed");
    }

    [Fact]
    public async Task Other_customers_cannot_retry_someone_elses_payment()
    {
        var product = await factory.CreateProductAsync(1_000m, 10);
        var orderId = await _customer.PlaceOrderAsync(product, "MPesa", Phone("84", "0001"));
        await _customer.OrderReachesAsync(orderId, "PaymentFailed");

        var stranger = await factory.CreateClientAs($"outro-{Guid.NewGuid():N}")
            .PostAsync(new Uri($"/api/orders/{orderId}/retry-payment", UriKind.Relative), null, Ct);

        stranger.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("sha256=0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Callbacks_without_a_valid_signature_are_rejected(string? signature)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["output_ResponseCode"] = "INS-0",
            ["output_TransactionID"] = "MP-FORGED",
            ["output_ThirdPartyReference"] = Guid.NewGuid().ToString(),
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/payments/callbacks/mpesa", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (signature is not null)
        {
            request.Headers.Add(WebhookSignature.Header, signature);
        }

        var response = await factory.CreateClient().SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_correctly_signed_callback_is_accepted_and_a_repeat_is_harmless()
    {
        var product = await factory.CreateProductAsync(1_000m, 10);
        var orderId = await _customer.PlaceOrderAsync(product, "MPesa", Phone("84"));
        await _customer.OrderReachesAsync(orderId, "Paid");
        var payment = (await factory.Backoffice.PaymentsAsync(orderId)).ShouldHaveSingleItem();
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["output_ResponseCode"] = "INS-0",
            ["output_TransactionID"] = payment.GetProperty("operatorTransactionId").GetString()!,
            ["output_ThirdPartyReference"] = payment.GetProperty("id").GetString()!,
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/payments/callbacks/mpesa", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WebhookSignature.Header, WebhookSignature.Sign(body, "dev-mpesa-callback-secret"));

        var response = await factory.CreateClient().SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _customer.OrderAsync(orderId)).GetProperty("status").GetString().ShouldBe("Paid");
    }
}
