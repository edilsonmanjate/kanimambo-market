using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Domain;

/// <summary>The amounts of an order, all in MZN: subtotal − promotion + shipping = total.</summary>
public sealed record OrderTotals(Money Subtotal, string PromotionName, Money Discount, Money ShippingFee)
{
    public Money Total => Subtotal - Discount + ShippingFee;
}
