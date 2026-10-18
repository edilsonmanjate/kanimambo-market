namespace Kanimambo.Ordering.Contracts;

public sealed record DispatchedParcelContract(Guid SellerId, string OriginCity, decimal WeightKg);
