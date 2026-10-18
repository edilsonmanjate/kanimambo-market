using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application;

public sealed record RemoveCartLine(Guid CartId, Guid ProductId) : ICommand<CartDto>;
