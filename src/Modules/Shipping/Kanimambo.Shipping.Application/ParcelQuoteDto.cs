namespace Kanimambo.Shipping.Application;

public sealed record ParcelQuoteDto(Guid SellerId, decimal Fee, string Courier, string? Vehicle, DateOnly EstimatedDelivery);
