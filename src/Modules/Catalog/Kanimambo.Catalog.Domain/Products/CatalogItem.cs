using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Domain.Products;

/// <summary>How many units of a leaf product one unit of an item needs.</summary>
public sealed record LeafRequirement(SimpleProduct Product, int Quantity);

/// <summary>
/// Composite component: anything that can be sold — a single product (leaf) or a bundle of items
/// (composite), which may itself contain bundles. Clients treat both uniformly: price, weight (for
/// shipping), stock and the IVA breakdown are computed recursively.
/// </summary>
public abstract class CatalogItem : AggregateRoot<ProductId>
{
    public const int MaxSkuLength = 64;

    protected CatalogItem()
    {
    }

    protected CatalogItem(ProductId id, SellerId sellerId, string sku, string name, string category)
        : base(id)
    {
        SellerId = sellerId;
        Sku = sku;
        Name = name;
        Category = NormalizeCategory(category);
    }

    public SellerId SellerId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    /// <summary>Net price (excluding IVA) in the item's currency.</summary>
    public abstract Money Price { get; }

    public abstract decimal WeightKg { get; }

    /// <summary>Units that can be sold right now, derived from the leaf products.</summary>
    public abstract int AvailableStock { get; }

    /// <summary>Flattens the item to the leaf products and quantities it consumes.</summary>
    public abstract IEnumerable<LeafRequirement> LeafRequirements(int quantity);

    /// <summary>Net amounts per tax category; IVA is applied per component because rates can differ.</summary>
    public abstract IEnumerable<TaxableAmount> TaxableAmounts(int quantity);

    /// <summary>True when <paramref name="item"/> is this item or appears anywhere below it.</summary>
    public abstract bool Contains(CatalogItem item);

    /// <summary>Categories are stored lower-case so filters match regardless of how sellers typed them.</summary>
    public static string NormalizeCategory(string category) => category.Trim().ToLowerInvariant();

    protected void Rename(string name, string category)
    {
        Name = name;
        Category = NormalizeCategory(category);
    }

    protected static List<FieldError> ValidateIdentity(string? sku, string? name, string? category)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(sku) || sku.Length > MaxSkuLength)
        {
            errors.Add(new FieldError("sku", $"SKU is required (max {MaxSkuLength} characters)."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new FieldError("name", "Name is required."));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors.Add(new FieldError("category", "Category is required."));
        }

        return errors;
    }
}
