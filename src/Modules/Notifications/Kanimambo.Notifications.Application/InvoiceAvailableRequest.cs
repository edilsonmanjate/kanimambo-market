using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application;

public sealed record InvoiceAvailableRequest(long Number, Guid DocumentId, string DocumentType, string DocumentNumber, string IssuerName, decimal Total)
    : MessageRequest(Number)
{
    private static readonly IReadOnlySet<ChannelKind> WithLinks = new HashSet<ChannelKind> { ChannelKind.Email, ChannelKind.WhatsApp };

    /// <summary>Several documents per order would flood an SMS inbox, and a PDF link needs a smartphone or a computer.</summary>
    public override IReadOnlySet<ChannelKind> AllowedChannels => WithLinks;

    public override CustomerNotification For(INotificationChannel channel, NotificationSettings settings) =>
        new InvoiceAvailable(channel, DocumentType == "CreditNote" ? "Nota de crédito" : "Fatura", DocumentNumber, IssuerName, Total, settings.DocumentLink(DocumentId));
}
