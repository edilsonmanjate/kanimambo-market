using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>Order paid: it will be prepared and delivered.</summary>
public sealed class OrderConfirmation(INotificationChannel channel, long orderNumber, decimal total, string deliveryMethod, Uri link)
    : CustomerNotification(channel)
{
    private static readonly Dictionary<string, string> Delivery = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SameDay"] = "Entrega no próprio dia.",
        ["Courier"] = "Envio por transportadora.",
        ["Pickup"] = "Avisamos quando estiver pronta para levantar.",
    };

    public override string Type => nameof(OrderConfirmation);

    public override MessageContent Compose() => new(
        $"Encomenda KM-{orderNumber} confirmada",
        $"Kanimambo! Recebemos o pagamento da encomenda KM-{orderNumber} ({Mzn.Of(total)}).",
        $"Os vendedores já estão a preparar a sua encomenda. {Delivery.GetValueOrDefault(deliveryMethod, "Avisamos quando sair para entrega.")}",
        link);
}
