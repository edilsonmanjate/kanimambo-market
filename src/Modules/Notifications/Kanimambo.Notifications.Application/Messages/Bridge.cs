using System.Globalization;
using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>What a channel reports back: the provider's message id and the text exactly as it was sent.</summary>
public sealed record ChannelReceipt(string ProviderMessageId, string RenderedText);

/// <summary>A delivery failure; transient ones (gateway down) are worth retrying later, the others are not.</summary>
public sealed record ChannelError : Error
{
    public ChannelError(ChannelKind channel, string code, string message, bool transient)
        : base($"notifications.{channel.ToString().ToLowerInvariant()}_{code}", message, transient ? ErrorType.Unavailable : ErrorType.BusinessRule)
    {
        Channel = channel;
        IsTransient = transient;
    }

    public ChannelKind Channel { get; }

    public bool IsTransient { get; }
}

/// <summary>
/// Bridge — the <em>implementor</em> side: a way to deliver any message. Each channel decides how content is rendered
/// for it (an SMS keeps the headline, in GSM-7, within 160 characters; WhatsApp adds the details; e-mail builds a
/// page) and talks to its own gateway. Channels know nothing about orders, payments or invoices.
/// </summary>
public interface INotificationChannel
{
    ChannelKind Kind { get; }

    Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, MessageContent content, CancellationToken cancellationToken);
}

/// <summary>
/// Bridge — the <em>abstraction</em> side: a kind of message the business sends. It decides what to say and holds a
/// reference to the channel that says it. Message types and channels vary independently: 4 messages × 3 channels
/// are 7 classes, not 12, and a new channel (USSD, push) or a new message is one more class.
/// </summary>
public abstract class CustomerNotification(INotificationChannel channel)
{
    protected INotificationChannel Channel { get; } = channel;

    public abstract string Type { get; }

    public abstract MessageContent Compose();

    public Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, CancellationToken cancellationToken) =>
        Channel.SendAsync(recipient, Compose(), cancellationToken);
}

/// <summary>Amounts as Mozambicans read them: <c>4 583,10 MZN</c>.</summary>
public static class Mzn
{
    private static readonly NumberFormatInfo Format = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = ",", NumberGroupSizes = [3] };

    public static string Of(decimal amount) => $"{amount.ToString("#,##0.00", Format)} MZN";
}

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
