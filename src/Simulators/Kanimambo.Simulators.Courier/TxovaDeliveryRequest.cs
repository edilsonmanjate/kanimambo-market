namespace Kanimambo.Simulators.Courier;

public sealed record TxovaDeliveryRequest(string Reference, TxovaPlace Pickup, TxovaPlace Dropoff, decimal WeightKg, string Vehicle, decimal CashToCollect, Uri WebhookUrl);
