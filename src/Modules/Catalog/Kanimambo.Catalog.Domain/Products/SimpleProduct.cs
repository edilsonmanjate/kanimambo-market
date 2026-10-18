using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Domain.Products;

/// <summary>Composite leaf: one sellable SKU with its own price, stock and weight.</summary>
public sealed class SimpleProduct : CatalogItem
{
    private SimpleProduct()
    {
    }

    private SimpleProduct(ProductId id, SellerId sellerId, ProductDetails details)
        : base(id, sellerId, details.Sku.Trim(), details.Name.Trim(), details.Category.Trim())
    {
        Apply(details);
    }

    public Money UnitPrice { get; private set; } = Money.Zero(Currency.Mzn);

    public int Stock { get; private set; }

    public decimal UnitWeightKg { get; private set; }

    public TaxCategory TaxCategory { get; private set; } = null!;

    /// <summary>Shared flyweight instance (see <see cref="VariantAttributesPool"/>).</summary>
    public VariantAttributes Variant { get; private set; } = null!;

    public override Money Price => UnitPrice;

    public override decimal WeightKg => UnitWeightKg;

    public override int AvailableStock => Stock;

    public static Result<SimpleProduct> Create(ProductId id, SellerId sellerId, ProductDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        return errors.Count > 0 ? new ValidationError(errors) : new SimpleProduct(id, sellerId, details);
    }

    /// <summary>Applies new details (e.g. from a catalog import) to an existing SKU.</summary>
    public Result Update(ProductDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        if (errors.Count > 0)
        {
            return new ValidationError(errors);
        }

        Rename(details.Name.Trim(), details.Category.Trim());
        Apply(details);
        return Result.Success();
    }

    public Result ChangePrice(Money newPrice)
    {
        ArgumentNullException.ThrowIfNull(newPrice);
        if (newPrice.Amount <= 0m)
        {
            return Error.BusinessRule("catalog.price_not_positive", $"Price of '{Sku}' must be positive.");
        }

        UnitPrice = newPrice;
        return Result.Success();
    }

    /// <summary>Takes units out of stock for a paid order; returns how many could not be covered (oversold).</summary>
    public int Reserve(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        var reserved = Math.Min(quantity, Stock);
        Stock -= reserved;
        return quantity - reserved;
    }

    public override IEnumerable<LeafRequirement> LeafRequirements(int quantity)
    {
        yield return new LeafRequirement(this, quantity);
    }

    public override IEnumerable<TaxableAmount> TaxableAmounts(int quantity)
    {
        yield return new TaxableAmount(TaxCategory, UnitPrice * quantity);
    }

    public override bool Contains(CatalogItem item) => ReferenceEquals(this, item) || Id == item?.Id;

    private void Apply(ProductDetails details)
    {
        UnitPrice = details.Price;
        Stock = details.Stock;
        UnitWeightKg = details.WeightKg;
        TaxCategory = details.TaxCategory;
        Variant = details.Variant;
    }

    /// <summary>The invariants of a sellable SKU, exposed so imports can report them before persisting.</summary>
    public static List<FieldError> Validate(ProductDetails details)
    {
        var errors = ValidateIdentity(details.Sku, details.Name, details.Category);
        if (details.Price is null || details.Price.Amount <= 0m)
        {
            errors.Add(new FieldError("price", "Price must be positive."));
        }

        if (details.Stock < 0)
        {
            errors.Add(new FieldError("stock", "Stock cannot be negative."));
        }

        if (details.WeightKg <= 0m)
        {
            errors.Add(new FieldError("weightKg", "Weight must be positive (it is used to price shipping)."));
        }

        if (details.Variant is null)
        {
            errors.Add(new FieldError("variant", "Variant attributes are required."));
        }

        if (details.TaxCategory is null)
        {
            errors.Add(new FieldError("taxCategory", "Tax category is required."));
        }

        return errors;
    }
}
