using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Promotions.Domain;

/// <summary>What a promotion gives: a percentage of the subtotal, or a fixed MZN amount (never more than the subtotal).</summary>
public sealed record PromotionDiscount
{
    private PromotionDiscount(DiscountKind kind, decimal value)
    {
        Kind = kind;
        Value = value;
    }

    public DiscountKind Kind { get; }

    public decimal Value { get; }

    public static Result<PromotionDiscount> Create(DiscountKind kind, decimal value) => kind switch
    {
        DiscountKind.Percentage when value is > 0m and <= 100m => new PromotionDiscount(kind, value),
        DiscountKind.FixedAmount when value > 0m => new PromotionDiscount(kind, Money.Mzn(value).Amount),
        _ => Error.Validation(new FieldError("discount", kind == DiscountKind.Percentage
            ? "A percentage discount must be greater than 0 and at most 100."
            : "A fixed discount must be a positive MZN amount.")),
    };

    public Money AmountFor(Money subtotal)
    {
        ArgumentNullException.ThrowIfNull(subtotal);
        var amount = Kind == DiscountKind.Percentage ? subtotal * (Value / 100m) : Money.Of(Value, subtotal.Currency);
        return amount > subtotal ? subtotal : amount;
    }

    public override string ToString() => Kind == DiscountKind.Percentage ? $"{Value:0.##}%" : $"{Value:0.00} MZN";
}
