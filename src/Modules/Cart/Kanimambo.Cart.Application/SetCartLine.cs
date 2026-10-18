using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application;

public sealed record SetCartLine(Guid CartId, Guid ProductId, int Quantity) : ICommand<CartDto>;
