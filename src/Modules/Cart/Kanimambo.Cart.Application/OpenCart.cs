using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application;

public sealed record OpenCart(string DeliveryCity) : ICommand<CartDto>;
