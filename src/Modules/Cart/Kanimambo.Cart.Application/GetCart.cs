using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application;

public sealed record GetCart(Guid CartId) : IQuery<CartDto>;
