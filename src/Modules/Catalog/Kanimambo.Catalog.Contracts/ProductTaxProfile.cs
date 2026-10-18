namespace Kanimambo.Catalog.Contracts;

/// <summary>How a product's price splits across tax categories (fractions add up to 1).</summary>
public sealed record ProductTaxProfile(Guid ProductId, IReadOnlyList<TaxShare> Shares);
