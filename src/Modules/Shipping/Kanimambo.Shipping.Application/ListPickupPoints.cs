using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Shipping.Application;

public sealed record ListPickupPoints(string? City) : IQuery<IReadOnlyList<PickupPointDto>>;
