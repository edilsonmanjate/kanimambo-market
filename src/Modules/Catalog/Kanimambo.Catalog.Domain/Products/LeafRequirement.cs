namespace Kanimambo.Catalog.Domain.Products;

/// <summary>How many units of a leaf product one unit of an item needs.</summary>
public sealed record LeafRequirement(SimpleProduct Product, int Quantity);
