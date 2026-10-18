using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.Notifications.Infrastructure.Channels;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.UnitTests.Bridge;

/// <summary>Every message works on every channel: the two hierarchies vary independently.</summary>
public sealed class BridgeTests
{
    private static readonly Uri Link = new("https://kanimambo.co.mz/e/KM-1004");

    private static readonly Func<INotificationChannel, CustomerNotification>[] Messages =
    [
        c => new OrderConfirmation(c, 1004, 4_583.10m, "SameDay", Link),
        c => new PaymentReminder(c, 1004, "InsufficientBalance", Link),
        c => new DeliveryUpdate(c, 1004, "ReadyForCollection", "Txova", "TX-123456", Link),
        c => new InvoiceAvailable(c, "Fatura", "FT 2026/000001", "Grossista da Matola, SA", 3_992.85m, Link),
    ];

    public static TheoryData<int, ChannelKind> Combinations()
    {
        var data = new TheoryData<int, ChannelKind>();
        for (var message = 0; message < Messages.Length; message++)
        {
            foreach (var channel in Enum.GetValues<ChannelKind>())
            {
                data.Add(message, channel);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public async Task Any_message_can_be_sent_through_any_channel(int message, ChannelKind channel)
    {
        var recording = new RecordingChannel(channel);
        var notification = Messages[message](recording);

        var receipt = await notification.SendAsync(new Recipient("258841234567", "ana@example.co.mz"), CancellationToken.None);

        receipt.Value.RenderedText.ShouldNotBeNullOrWhiteSpace();
        recording.Received.ShouldHaveSingleItem().Subject.ShouldContain(message == 3 ? "FT 2026/000001" : "KM-1004");
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Each_channel_renders_every_message_within_its_rules(int message, ChannelKind channel)
    {
        var content = Messages[message](new RecordingChannel(channel)).Compose();

        switch (channel)
        {
            case ChannelKind.Sms:
                var sms = SmsChannel.Render(content);
                Gsm7Text.Length(sms).ShouldBeLessThanOrEqualTo(160);
                Gsm7Text.Convert(sms).ShouldBe(sms); // nothing outside GSM-7
                break;
            case ChannelKind.WhatsApp:
                WhatsAppChannel.Render(content).ShouldStartWith($"*{content.Subject}*");
                break;
            default:
                EmailChannel.Render(content).ShouldContain("<a href=\"https://kanimambo.co.mz/e/KM-1004\">");
                break;
        }
    }

    [Fact]
    public void An_sms_keeps_the_essentials_drops_accents_and_adds_the_link_when_it_fits()
    {
        var content = new OrderConfirmation(new RecordingChannel(ChannelKind.Sms), 1004, 4_583.10m, "SameDay", Link).Compose();

        SmsChannel.Render(content).ShouldBe("Kanimambo! Recebemos o pagamento da encomenda KM-1004 (4 583,10 MZN). kanimambo.co.mz/e/KM-1004");
        WhatsAppChannel.Render(content).ShouldContain("Os vendedores já estão a preparar a sua encomenda. Entrega no próprio dia.");
    }

    [Fact]
    public void A_headline_too_long_for_one_sms_is_shortened_at_a_word()
    {
        var content = new MessageContent("x", string.Join(' ', Enumerable.Repeat("Informação importante sobre a encomenda", 8)), "x", Link);

        var sms = SmsChannel.Render(content);

        Gsm7Text.Length(sms).ShouldBeLessThanOrEqualTo(160);
        sms.ShouldEndWith("...");
        sms.ShouldStartWith("Informacao importante");
        sms.ShouldNotContain("kanimambo.co.mz");
    }

    [Theory]
    [InlineData("Ação já disponível — confirmação", "Acao ja disponivel - confirmacao")]
    [InlineData("Café é bom, Çá", "Café é bom, Ça")]   // é and Ç are GSM-7; á is not
    [InlineData("Preço: 10 €", "Preco: 10 €")]
    [InlineData("ẞ😀", "??")]
    public void Text_is_converted_to_the_GSM_7_alphabet(string text, string expected) => Gsm7Text.Convert(text).ShouldBe(expected);

    [Fact]
    public void Extension_characters_count_twice() => Gsm7Text.Length("€[]").ShouldBe(6);

    [Fact]
    public void E_mail_content_is_html_encoded()
    {
        var html = EmailChannel.Render(new MessageContent("<b>", "Loja <script>alert(1)</script>", "&", null));

        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;");
    }

    [Theory]
    [InlineData(4_583.10, "4 583,10 MZN")]
    [InlineData(95, "95,00 MZN")]
    [InlineData(1_250_000.5, "1 250 000,50 MZN")]
    public void Amounts_are_written_the_Mozambican_way(decimal amount, string expected) => Mzn.Of(amount).ShouldBe(expected);

    private sealed class RecordingChannel(ChannelKind kind) : INotificationChannel
    {
        public List<MessageContent> Received { get; } = [];

        public ChannelKind Kind => kind;

        public Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, MessageContent content, CancellationToken cancellationToken)
        {
            Received.Add(content);
            return Task.FromResult(Result.Success(new ChannelReceipt("id-1", content.Headline)));
        }
    }
}
