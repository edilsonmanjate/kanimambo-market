using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application;

public sealed record ChangeDeliveryCity(Guid CartId, string City) : ICommand<CartDto>;
