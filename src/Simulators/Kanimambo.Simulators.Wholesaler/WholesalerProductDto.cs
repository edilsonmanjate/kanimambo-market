namespace Kanimambo.Simulators.Wholesaler;

public sealed record WholesalerProductDto(
    string Code,
    string Description,
    string Category,
    string UnitCode,
    long PriceCents,
    string Currency,
    int Stock,
    int WeightGrams,
    string TaxCategory);
