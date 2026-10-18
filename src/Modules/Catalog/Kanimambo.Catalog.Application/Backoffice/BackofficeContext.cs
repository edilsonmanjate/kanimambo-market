using Kanimambo.Catalog.Application.Abstractions;

namespace Kanimambo.Catalog.Application.Backoffice;

/// <summary>Services an operation needs to run; passed in at execution time so operations stay serializable.</summary>
public sealed class BackofficeContext(ISellerRepository sellers, ICatalogItemRepository products, TimeProvider timeProvider)
{
    public ISellerRepository Sellers { get; } = sellers;

    public ICatalogItemRepository Products { get; } = products;

    public TimeProvider TimeProvider { get; } = timeProvider;
}
