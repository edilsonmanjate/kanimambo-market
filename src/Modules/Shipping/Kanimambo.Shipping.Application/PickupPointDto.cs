namespace Kanimambo.Shipping.Application;

public sealed record PickupPointDto(string Id, string Name, string City, string Address, decimal MaxKg);
