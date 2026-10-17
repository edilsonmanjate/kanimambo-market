using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.UnitTests;

internal static class TestOrders
{
    public static readonly DateTimeOffset Now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    public static readonly Guid Seller = Guid.NewGuid();

    public static OrderLine Line(string sku, decimal unitPrice, int quantity) =>
        new(Guid.NewGuid(), Seller, sku, $"Produto {sku}", quantity, Money.Mzn(unitPrice), Money.Mzn(unitPrice * quantity));

    public static Order Place(PaymentMethod method = PaymentMethod.MPesa, string customerType = "registered", params OrderLine[] lines)
    {
        var orderLines = lines.Length > 0 ? lines : [Line("ARROZ-25", 1_850m, 1), Line("OLEO-5", 720m, 2)];
        var subtotal = orderLines.Select(l => l.LineTotal).Aggregate((a, b) => a + b);
        return Order.Place(OrderId.New(), 1001, "cart", Guid.NewGuid(),
            new OrderCustomer("restaurante-polana", customerType, "258841234567", customerType == "b2b" ? "400123456" : ""),
            "Maputo", new OrderDelivery("SameDay", "KaMpfumo", null, DateOnly.FromDateTime(Now.UtcDateTime)), [new OrderParcel(Seller, "Maputo", 27m)],
            method, orderLines, new OrderTotals(subtotal, "", Money.Zero(Currency.Mzn), Money.Mzn(150m)), Now).Value;
    }

    /// <summary>Applies an action with representative arguments.</summary>
    public static SharedKernel.Results.Result Apply(this Order order, OrderAction action) => action switch
    {
        OrderAction.RequestPayment => order.RequestPayment(Now),
        OrderAction.ConfirmPayment => order.ConfirmPayment("MP-REF-001", Now),
        OrderAction.FailPayment => order.FailPayment("INS-6 saldo insuficiente", Now),
        OrderAction.RetryPayment => order.RetryPayment(Now),
        OrderAction.StartPreparing => order.StartPreparing(Now),
        OrderAction.Dispatch => order.Dispatch(Now),
        OrderAction.Deliver => order.Deliver(Now),
        OrderAction.Cancel => order.Cancel("Cliente desistiu", Now),
        OrderAction.Refund => order.Refund("Produto danificado", Now),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}
