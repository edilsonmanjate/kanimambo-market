using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Products;

public sealed class CreateBundleHandler(ISellerRepository sellers, ICatalogItemRepository products)
    : IRequestHandler<CreateBundle, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateBundle request, CancellationToken cancellationToken)
    {
        var seller = await sellers.GetAsync(new SellerId(request.SellerId), cancellationToken);
        if (seller is null)
        {
            return CatalogErrors.SellerNotFound(request.SellerId);
        }

        if (!seller.IsActive)
        {
            return CatalogErrors.SellerSuspended(seller.Name);
        }

        if (await products.SkuExistsAsync(seller.Id, request.Sku, cancellationToken))
        {
            return Error.Conflict("catalog.sku_taken", $"SKU '{request.Sku}' already exists for this seller.");
        }

        var ids = request.Components.Select(c => new ProductId(c.ProductId)).Distinct().ToArray();
        var items = (await products.GetTreesAsync(ids, cancellationToken)).ToDictionary(i => i.Id);

        var missing = ids.FirstOrDefault(id => !items.ContainsKey(id));
        if (missing != default)
        {
            return CatalogErrors.ProductNotFound(missing.Value);
        }

        var bundle = Bundle.Create(
            ProductId.New(),
            seller.Id,
            request.Sku,
            request.Name,
            request.Category,
            request.Components.Select(c => (items[new ProductId(c.ProductId)], c.Quantity)).ToList());

        if (bundle.IsFailure)
        {
            return bundle.Error;
        }

        products.Add(bundle.Value);
        return bundle.Value.Id.Value;
    }
}
