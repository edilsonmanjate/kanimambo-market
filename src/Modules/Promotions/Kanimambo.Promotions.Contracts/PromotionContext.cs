using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Promotions.Contracts;

/// <summary>What the promotion rules may look at for one cart.</summary>
public sealed record PromotionContext(
    Money CartTotal,
    int ItemCount,
    IReadOnlyCollection<string> Categories,
    string CustomerCity,
    string CustomerType,
    PaymentMethod PaymentMethod);
