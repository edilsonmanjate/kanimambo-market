using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Cart.Application.Pricing;

public sealed record PriceCart(Guid CartId, PaymentMethod PaymentMethod = PaymentMethod.None) : IQuery<CartPricingDto>;
