using Kanimambo.Notifications.Domain;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>A payment attempt failed: why, and that the customer can try again.</summary>
public sealed class PaymentReminder(INotificationChannel channel, long orderNumber, string reason, Uri link) : CustomerNotification(channel)
{
    private static readonly Dictionary<string, string> Reasons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["InsufficientBalance"] = "saldo insuficiente",
        ["Timeout"] = "o PIN não foi confirmado a tempo",
        ["InvalidNumber"] = "o número não tem carteira móvel",
        ["DuplicateTransaction"] = "transação duplicada",
        ["UnknownOperator"] = "operadora desconhecida",
        ["WalletMismatch"] = "o número não pertence à carteira escolhida",
        ["GatewayUnavailable"] = "a operadora está indisponível",
    };

    public override string Type => nameof(PaymentReminder);

    public override MessageContent Compose() => new(
        $"Pagamento da encomenda KM-{orderNumber} não concluído",
        $"O pagamento da encomenda KM-{orderNumber} não foi concluído: {Reasons.GetValueOrDefault(reason, "pagamento recusado")}.",
        "A encomenda fica reservada. Pode tentar pagar de novo na app ou escolher outro método.",
        link);
}
