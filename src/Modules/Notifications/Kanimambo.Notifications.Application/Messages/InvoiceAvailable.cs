using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>An invoice or credit note was issued. Only sent where a link to a document makes sense (not by SMS).</summary>
public sealed class InvoiceAvailable(INotificationChannel channel, string documentTitle, string number, string issuerName, decimal total, Uri link)
    : CustomerNotification(channel)
{
    public override string Type => nameof(InvoiceAvailable);

    public override MessageContent Compose() => new(
        $"{documentTitle} {number} — {issuerName}",
        $"{documentTitle} {number} de {issuerName}: {Mzn.Of(total)}.",
        "O documento fiscal está disponível para descarregar.",
        link);
}
