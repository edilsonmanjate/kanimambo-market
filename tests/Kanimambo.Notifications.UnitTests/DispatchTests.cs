using Kanimambo.Notifications.Application;
using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kanimambo.Notifications.UnitTests;

/// <summary>Choosing the channel at send time: preferences, what each message allows, and fallback.</summary>
public sealed class DispatchTests
{
    private const string Phone = "258841234567";
    private static readonly NotificationSettings Settings = new(new Uri("https://kanimambo.co.mz/"), [ChannelKind.Sms]);

    private readonly Preferences _preferences = new();
    private readonly SentLog _sent = new();
    private readonly Dictionary<ChannelKind, FakeChannel> _channels = Enum.GetValues<ChannelKind>().ToDictionary(k => k, k => new FakeChannel(k));

    private NotificationHandlers Handlers => new(_preferences, _sent, new NotificationChannels(_channels.Values), Settings, new Actor("ana"),
        new FakeTimeProvider(), NullLogger<NotificationHandlers>.Instance);

    [Fact]
    public async Task Without_preferences_the_customer_gets_an_sms()
    {
        await Handlers.Handle(Notify(new OrderConfirmationRequest(1004, 950m, "SameDay")), CancellationToken.None);

        _channels[ChannelKind.Sms].Sent.ShouldBe(1);
        _sent.Items.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            n => n.Channel.ShouldBe(ChannelKind.Sms),
            n => n.Status.ShouldBe(DeliveryStatus.Sent),
            n => n.MessageType.ShouldBe(nameof(OrderConfirmation)));
    }

    [Fact]
    public async Task The_first_preferred_channel_that_works_is_used_and_failures_are_recorded()
    {
        _preferences.Add(ContactPreferences.Create(Phone, "ana", null, [ChannelKind.WhatsApp, ChannelKind.Sms], DateTimeOffset.UtcNow).Value);
        _channels[ChannelKind.WhatsApp].FailWith = new ChannelError(ChannelKind.WhatsApp, "not_on_whatsapp", "Not on WhatsApp.", transient: false);

        var result = await Handlers.Handle(Notify(new PaymentReminderRequest(1004, "Timeout")), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _sent.Items.Select(n => (n.Channel, n.Status)).ShouldBe([(ChannelKind.WhatsApp, DeliveryStatus.Failed), (ChannelKind.Sms, DeliveryStatus.Sent)]);
    }

    [Fact]
    public async Task Invoices_never_go_by_sms()
    {
        var result = await Handlers.Handle(Notify(new InvoiceAvailableRequest(1004, Guid.NewGuid(), "Invoice", "FT 2026/000001", "Loja", 100m)), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _channels[ChannelKind.Sms].Sent.ShouldBe(0);
        _sent.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task E_mail_is_only_used_with_an_address()
    {
        _preferences.Add(ContactPreferences.Create(Phone, "ana", "ana@example.co.mz", [ChannelKind.Email, ChannelKind.Sms], DateTimeOffset.UtcNow).Value);

        await Handlers.Handle(Notify(new InvoiceAvailableRequest(1004, Guid.NewGuid(), "Invoice", "FT 2026/000001", "Loja", 100m)), CancellationToken.None);

        _sent.Items.ShouldHaveSingleItem().Recipient.ShouldBe("ana@example.co.mz");
    }

    [Fact]
    public async Task When_every_channel_is_temporarily_down_the_command_fails_so_it_is_retried_later()
    {
        _channels[ChannelKind.Sms].FailWith = new ChannelError(ChannelKind.Sms, "unavailable", "503", transient: true);

        var result = await Handlers.Handle(Notify(new OrderConfirmationRequest(1004, 950m, "SameDay")), CancellationToken.None);

        result.Error.ShouldBeOfType<ChannelError>().IsTransient.ShouldBeTrue();
    }

    [Fact]
    public async Task A_permanent_refusal_is_recorded_and_not_retried()
    {
        _channels[ChannelKind.Sms].FailWith = new ChannelError(ChannelKind.Sms, "not_gsm7", "Bad text", transient: false);

        var result = await Handlers.Handle(Notify(new OrderConfirmationRequest(1004, 950m, "SameDay")), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _sent.Items.ShouldHaveSingleItem().Status.ShouldBe(DeliveryStatus.Failed);
    }

    [Theory]
    [InlineData("841234567", null, new[] { ChannelKind.Sms }, "phone")]
    [InlineData(Phone, null, new ChannelKind[0], "channels")]
    [InlineData(Phone, null, new[] { ChannelKind.Email }, "email")]
    [InlineData(Phone, "not-an-email", new[] { ChannelKind.Sms }, "email")]
    [InlineData(Phone, null, new[] { ChannelKind.Sms, ChannelKind.Sms }, "channels")]
    public void Preferences_are_validated(string phone, string? email, ChannelKind[] channels, string field) =>
        ContactPreferences.Create(phone, "ana", email, channels, DateTimeOffset.UtcNow).Error
            .ShouldBeOfType<ValidationError>().Errors.ShouldContain(e => e.Field == field);

    private static Notify Notify(MessageRequest message) => new(Guid.NewGuid(), Phone, message);

    private sealed class FakeChannel(ChannelKind kind) : INotificationChannel
    {
        public ChannelError? FailWith { get; set; }

        public int Sent { get; private set; }

        public ChannelKind Kind => kind;

        public Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, MessageContent content, CancellationToken cancellationToken)
        {
            if (FailWith is not null)
            {
                return Task.FromResult(Result<ChannelReceipt>.Failure(FailWith));
            }

            Sent++;
            return Task.FromResult(Result.Success(new ChannelReceipt($"{kind}-1", content.Headline)));
        }
    }

    private sealed class Preferences : IContactPreferencesRepository
    {
        private readonly Dictionary<string, ContactPreferences> _items = [];

        public void Add(ContactPreferences preferences) => _items[preferences.Phone] = preferences;

        public Task<ContactPreferences?> GetAsync(string phone, CancellationToken cancellationToken) => Task.FromResult(_items.GetValueOrDefault(phone));
    }

    private sealed class SentLog : ISentNotificationRepository
    {
        public List<SentNotification> Items { get; } = [];

        public void Add(SentNotification notification) => Items.Add(notification);

        public Task<IReadOnlyList<SentNotification>> ListAsync(string? recipient, long? orderNumber, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SentNotification>>(Items);
    }

    private sealed class Actor(string name) : ICurrentActor
    {
        public string Name => name;

        public bool IsAuthenticated => true;

        public bool IsInRole(string role) => false;
    }
}
