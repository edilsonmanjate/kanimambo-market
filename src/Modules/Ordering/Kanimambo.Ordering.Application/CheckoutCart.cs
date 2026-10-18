using Kanimambo.Ordering.Application.Checkout;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

/// <summary>Checkout a cart. Retried requests return the same order (see <see cref="CheckoutResult.AlreadyPlaced"/>).</summary>
public sealed record CheckoutCart(Guid CartId, PaymentMethod PaymentMethod, string Phone, string? Nuit, DeliveryChoice? Delivery = null) : ICommand<CheckoutResult>;
