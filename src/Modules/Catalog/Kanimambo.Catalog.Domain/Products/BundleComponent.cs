namespace Kanimambo.Catalog.Domain.Products;

/// <summary>One line of a bundle: an item (product or bundle) and how many of it.</summary>
public sealed class BundleComponent
{
    private BundleComponent()
    {
    }

    internal BundleComponent(CatalogItem component, int quantity)
    {
        Component = component;
        ComponentId = component.Id;
        Quantity = quantity;
    }

    public ProductId ComponentId { get; private set; }

    public CatalogItem Component { get; private set; } = null!;

    public int Quantity { get; private set; }

    internal void Increase(int quantity) => Quantity += quantity;
}
