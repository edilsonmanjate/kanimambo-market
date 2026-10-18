using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application;

/// <summary>Goes back to the previous snapshot (discarding the latest change).</summary>
public sealed record UndoCartChange(Guid CartId) : ICommand<CartDto>;
