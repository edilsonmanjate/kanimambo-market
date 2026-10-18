using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.Notifications.Infrastructure.Channels;
using Kanimambo.Simulators.Messaging;
using static Kanimambo.Notifications.IntegrationTests.Journey;

namespace Kanimambo.Notifications.IntegrationTests;

/// <summary>Business events (outbox) → message type (abstraction) × the customer's channel (implementor) → simulated providers.</summary>
public sealed class NotificationTests(NotificationsApiFactory factory)
{
    private static readonly string[] EmailThenSms = ["Email", "Sms"];
    private static readonly string[] WhatsAppOnly = ["WhatsApp"];
    private static readonly string[] EmailOnly = ["Email"];
    private static readonly string[] SmsOnly = ["Sms"];
    private static readonly string[] Pigeon = ["Pigeon"];

    private readonly string _user = $"cliente-{Guid.NewGuid():N}";
    private HttpClient Customer => factory.CreateClientAs(_user);

    private static bool IsConfirmation(JsonElement sent) => sent.GetProperty("messageType").GetString() == "OrderConfirmation";

    private async Task<JsonElement[]> SentAsync(string recipient) =>
        (await factory.Backoffice.GetFromJsonAsync<JsonElement[]>(new Uri($"/api/notifications/sent?recipient={recipient}", UriKind.Relative), Ct))!;

    private static bool Confirms(DeliveredMessage message) => message.Text.Contains("Recebemos o pagamento", StringComparison.Ordinal);

    [Fact]
    public async Task A_paid_order_is_confirmed_by_sms_in_one_gsm_7_message()
    {
        var phone = Phone();
        var product = await factory.CreateProductAsync(950m);
        var order = await Customer.PlaceAsync(product, "MPesa", phone);

        await factory.Backoffice.TransitionAsync(order.GetProperty("orderId").GetGuid(), "ConfirmPayment", "MP-1");

        var sms = (await EventuallyAsync(() => factory.InboxAsync(phone), m => m.Length > 0, "the customer got an SMS")).ShouldHaveSingleItem();
        sms.Channel.ShouldBe("sms");
        sms.Text.ShouldStartWith($"Kanimambo! Recebemos o pagamento da encomenda {order.GetProperty("displayNumber").GetString()}");
        Gsm7Text.Length(sms.Text).ShouldBeLessThanOrEqualTo(160);

        // The provider has the message before our log commits (same unit of work, a moment later): wait for the log too.
        var log = await EventuallyAsync(() => SentAsync(phone), l => l.Length > 0, "the delivery was logged");
        log.ShouldHaveSingleItem().GetProperty("messageType").GetString().ShouldBe("OrderConfirmation");
    }

    [Fact]
    public async Task A_failed_payment_sends_a_reminder_with_the_reason()
    {
        var phone = Phone();
        var order = await Customer.PlaceAsync(await factory.CreateProductAsync(400m), "MPesa", phone);

        await factory.Backoffice.TransitionAsync(order.GetProperty("orderId").GetGuid(), "FailPayment", "InsufficientBalance");

        var sms = (await EventuallyAsync(() => factory.InboxAsync(phone), m => m.Length > 0, "the reminder arrived")).ShouldHaveSingleItem();
        sms.Text.ShouldContain("nao foi concluido: saldo insuficiente"); // accents removed for GSM-7
    }

    [Fact]
    public async Task Customers_choose_their_channels_and_invoices_reach_them_by_e_mail()
    {
        var phone = Phone();
        var email = $"{Guid.NewGuid():N}@example.co.mz";
        await Customer.PutAsJsonAsync($"/api/notifications/preferences/{phone}", new { email, channels = EmailThenSms }, Ct).Json();
        var order = await Customer.PlaceAsync(await factory.CreateProductAsync(950m), "Card", phone);

        await factory.Backoffice.TransitionAsync(order.GetProperty("orderId").GetGuid(), "ConfirmPayment", "CARD-1");

        // Confirmation and the marketplace's delivery invoice (the seller has no fiscal profile yet, so no goods invoice).
        var mail = await EventuallyAsync(() => factory.InboxAsync(email), m => m.Length >= 2, "two e-mails arrived");
        mail.Select(m => m.Subject).ShouldContain(s => s!.StartsWith("Encomenda KM-", StringComparison.Ordinal));
        mail.Select(m => m.Subject).ShouldContain(s => s!.StartsWith("Fatura FT ", StringComparison.Ordinal) && s.Contains("Kanimambo Market", StringComparison.Ordinal));
        (await factory.InboxAsync(phone)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_whatsapp_or_e_mail_invoices_are_not_sent_by_sms()
    {
        var phone = Phone();
        var order = await Customer.PlaceAsync(await factory.CreateProductAsync(950m), "Card", phone);
        var number = order.GetProperty("number").GetInt64();

        await factory.Backoffice.TransitionAsync(order.GetProperty("orderId").GetGuid(), "ConfirmPayment", "CARD-1");

        await EventuallyAsync(() => factory.InboxAsync(phone), m => m.Length > 0, "the confirmation arrived");
        await Task.Delay(500, Ct);
        (await factory.InboxAsync(phone)).ShouldAllBe(m => m.Text.Contains("Recebemos o pagamento", StringComparison.Ordinal));
        var invoices = await factory.Backoffice.GetFromJsonAsync<JsonElement[]>(new Uri($"/api/invoicing/documents?orderId={order.GetProperty("orderId").GetGuid()}", UriKind.Relative), Ct);
        invoices.ShouldNotBeNull().ShouldNotBeEmpty();
        (await factory.Backoffice.GetFromJsonAsync<JsonElement[]>(new Uri($"/api/notifications/sent?orderNumber={number}", UriKind.Relative), Ct))
            .ShouldNotBeNull().ShouldAllBe(n => n.GetProperty("messageType").GetString() == "OrderConfirmation");
    }

    [Theory]
    [InlineData("0202", new[] { "WhatsApp", "Sms" }, "sms")]       // not on WhatsApp → SMS
    [InlineData("0201", new[] { "Sms", "WhatsApp" }, "whatsapp")]  // SMS gateway down → WhatsApp
    public async Task A_channel_that_cannot_deliver_falls_back_to_the_next_preference(string suffix, string[] channels, string expected)
    {
        var phone = Phone(suffix);
        await Customer.PutAsJsonAsync($"/api/notifications/preferences/{phone}", new { channels }, Ct).Json();
        var order = await Customer.PlaceAsync(await factory.CreateProductAsync(950m), "MPesa", phone);

        await factory.Backoffice.TransitionAsync(order.GetProperty("orderId").GetGuid(), "ConfirmPayment", "MP-2");

        var confirmation = (await EventuallyAsync(() => factory.InboxAsync(phone), m => m.Any(Confirms), "the confirmation arrived")).Where(Confirms).ShouldHaveSingleItem();
        confirmation.Channel.ShouldBe(expected);
        var log = await EventuallyAsync(() => SentAsync(phone), l => l.Count(IsConfirmation) >= 2, "both attempts were logged");
        log.Where(IsConfirmation)
            .Select(n => (n.GetProperty("channel").GetString(), n.GetProperty("status").GetString()))
            .ShouldBe([(channels[0], "Failed"), (channels[1], "Sent")]);
    }

    [Fact]
    public async Task Delivery_progress_reaches_the_customer()
    {
        var phone = Phone();
        var order = await Customer.PlaceAsync(await factory.CreateProductAsync(300m), "CashOnDelivery", phone);
        var orderId = order.GetProperty("orderId").GetGuid();

        await factory.Backoffice.TransitionAsync(orderId, "StartPreparing");
        await factory.Backoffice.TransitionAsync(orderId, "Dispatch");

        // Collected, delivered, and — cash being collected on delivery — the payment confirmation.
        var texts = (await EventuallyAsync(() => factory.InboxAsync(phone),
                m => m.Any(x => x.Text.Contains("foi entregue", StringComparison.Ordinal)) && m.Any(Confirms), "the delivery and payment were announced"))
            .Select(m => m.Text).ToList();
        texts.ShouldContain(t => t.Contains("esta a caminho", StringComparison.Ordinal));
        texts.ShouldContain(t => t.Contains("foi entregue", StringComparison.Ordinal));
        texts.ShouldContain(t => t.Contains("Recebemos o pagamento", StringComparison.Ordinal)); // cash collected on delivery
    }

    [Fact]
    public async Task Preferences_belong_to_the_account_that_registered_the_number()
    {
        var phone = Phone();
        await Customer.PutAsJsonAsync($"/api/notifications/preferences/{phone}", new { channels = WhatsAppOnly }, Ct).Json();

        var stranger = await factory.CreateClientAs($"outro-{Guid.NewGuid():N}")
            .PutAsJsonAsync($"/api/notifications/preferences/{phone}", new { email = "spam@example.com", channels = EmailOnly }, Ct);
        var anonymous = await factory.CreateClient().PutAsJsonAsync($"/api/notifications/preferences/{phone}", new { channels = SmsOnly }, Ct);
        var invalid = await Customer.PutAsJsonAsync($"/api/notifications/preferences/{phone}", new { channels = Pigeon }, Ct);

        stranger.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Customer.GetFromJsonAsync<JsonElement>(new Uri($"/api/notifications/preferences/{phone}", UriKind.Relative), Ct))
            .GetProperty("channels").EnumerateArray().Select(c => c.GetString()).ShouldBe(["WhatsApp"]);
    }

    [Fact]
    public async Task The_sms_gateway_rejects_what_does_not_fit_one_gsm_7_sms()
    {
        var gateway = factory.Messaging.CreateClient();
        gateway.DefaultRequestHeaders.Add(MessagingSimulator.ApiKeyHeader, "test");

        var accented = await gateway.PostAsJsonAsync("sms/v1/messages", new { to = Phone(), sender = "KANIMAMBO", text = "Ação" }, Ct);
        var tooLong = await gateway.PostAsJsonAsync("sms/v1/messages", new { to = Phone(), sender = "KANIMAMBO", text = new string('a', 161) }, Ct);

        (await accented.Content.ReadAsStringAsync(Ct)).ShouldContain("NOT_GSM7");
        (await tooLong.Content.ReadAsStringAsync(Ct)).ShouldContain("TOO_LONG");
    }
}
