using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>A parcel moved: collected, ready at the pickup point, delivered, or a failed attempt.</summary>
public sealed class DeliveryUpdate(INotificationChannel channel, long orderNumber, string status, string courier, string? trackingCode, Uri link)
    : CustomerNotification(channel)
{
    /// <summary>The parcel statuses worth telling the customer about; the others (booked, in transit) are silent.</summary>
    public static readonly IReadOnlyDictionary<string, string> Notified = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["PickedUp"] = "está a caminho",
        ["ReadyForCollection"] = "já pode ser levantada no ponto de recolha",
        ["Delivered"] = "foi entregue",
        ["Failed"] = "não pôde ser entregue",
    };

    public override string Type => nameof(DeliveryUpdate);

    public override MessageContent Compose() => new(
        $"Encomenda KM-{orderNumber}: {Notified[status]}",
        $"A sua encomenda KM-{orderNumber} {Notified[status]}.",
        status == "Failed"
            ? $"A transportadora ({courier}) vai contactá-lo para combinar nova entrega. Guia: {trackingCode ?? "—"}."
            : $"Transportadora: {courier}. Guia: {trackingCode ?? "—"}.",
        link);
}
