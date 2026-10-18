using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application.Checkout;

/// <summary>Closes the cart once its order exists. Idempotent: the event may be delivered more than once.</summary>
public sealed record CloseCheckedOutCart(Guid IdempotencyKey, Guid CartId, Guid OrderId) : IIdempotentCommand;
