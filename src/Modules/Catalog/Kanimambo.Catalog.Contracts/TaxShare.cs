namespace Kanimambo.Catalog.Contracts;

/// <summary>Part of a product's price that falls in one tax category; a bundle can mix exempt and taxed goods.</summary>
public sealed record TaxShare(string Category, decimal Fraction);
