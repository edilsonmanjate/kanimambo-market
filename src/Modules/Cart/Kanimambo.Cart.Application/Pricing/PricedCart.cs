using Kanimambo.Cart.Domain;
using Kanimambo.Promotions.Contracts;
using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Cart.Application.Pricing;

/// <summary>A priced cart. Customer, promotion and exchange rates are always objects — null objects when "none".</summary>
public sealed record PricedCart(
    Customer Customer,
    IReadOnlyList<PricedLine> Lines,
    Money Subtotal,
    IAppliedPromotion Promotion,
    Money Total);
