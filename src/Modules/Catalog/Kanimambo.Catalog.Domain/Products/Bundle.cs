using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Domain.Products;

/// <summary>
/// Composite: a kit such as "Cabaz do Mês" (rice, oil, sugar, maize flour). It may contain other
/// bundles; every figure is derived from its components, so it never goes stale.
/// </summary>
public sealed class Bundle : CatalogItem
{
    private readonly List<BundleComponent> _components = [];

    private Bundle()
    {
    }

    private Bundle(ProductId id, SellerId sellerId, string sku, string name, string category)
        : base(id, sellerId, sku, name, category)
    {
    }

    public IReadOnlyList<BundleComponent> Components => _components.AsReadOnly();

    /// <summary>Sum of the components' net prices. Promotions (Phase 2) apply discounts, not the bundle.</summary>
    public override Money Price => _components
        .Select(c => c.Component.Price * c.Quantity)
        .Aggregate((total, next) => total + next);

    public override decimal WeightKg => _components.Sum(c => c.Component.WeightKg * c.Quantity);

    /// <summary>
    /// The minimum, over every leaf product, of <c>stock / quantity needed per bundle</c>. Requirements
    /// are aggregated first: a product reached through two sub-bundles is needed twice, not once.
    /// </summary>
    public override int AvailableStock => LeafRequirements(1)
        .GroupBy(r => r.Product.Id)
        .Min(group => group.First().Product.Stock / group.Sum(r => r.Quantity));

    public static Result<Bundle> Create(
        ProductId id,
        SellerId sellerId,
        string sku,
        string name,
        string category,
        IReadOnlyCollection<(CatalogItem Item, int Quantity)> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var errors = ValidateIdentity(sku, name, category);
        if (components.Count == 0)
        {
            errors.Add(new FieldError("components", "A bundle needs at least one component."));
        }

        if (errors.Count > 0)
        {
            return new ValidationError(errors);
        }

        var bundle = new Bundle(id, sellerId, sku.Trim(), name.Trim(), category.Trim());
        foreach (var (item, quantity) in components)
        {
            var added = bundle.AddComponent(item, quantity);
            if (added.IsFailure)
            {
                return added.Error;
            }
        }

        return bundle;
    }

    public Result AddComponent(CatalogItem item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (quantity <= 0)
        {
            return Error.Validation(new FieldError("quantity", "Component quantity must be at least 1."));
        }

        if (item.SellerId != SellerId)
        {
            return Error.BusinessRule("catalog.bundle_mixed_sellers", $"'{item.Sku}' belongs to another seller.");
        }

        if (item.Contains(this))
        {
            return Error.BusinessRule("catalog.bundle_cycle", $"Adding '{item.Sku}' would make '{Sku}' contain itself.");
        }

        if (_components.Count > 0 && item.Price.Currency != _components[0].Component.Price.Currency)
        {
            return Error.BusinessRule("catalog.bundle_mixed_currencies", "All components of a bundle must be priced in the same currency.");
        }

        var existing = _components.Find(c => c.ComponentId == item.Id);
        if (existing is not null)
        {
            existing.Increase(quantity);
        }
        else
        {
            _components.Add(new BundleComponent(item, quantity));
        }

        return Result.Success();
    }

    public override IEnumerable<LeafRequirement> LeafRequirements(int quantity) =>
        _components.SelectMany(c => c.Component.LeafRequirements(quantity * c.Quantity));

    public override IEnumerable<TaxableAmount> TaxableAmounts(int quantity) =>
        _components.SelectMany(c => c.Component.TaxableAmounts(quantity * c.Quantity));

    public override bool Contains(CatalogItem item) =>
        item is not null && (Id == item.Id || _components.Exists(c => c.Component.Contains(item)));
}
