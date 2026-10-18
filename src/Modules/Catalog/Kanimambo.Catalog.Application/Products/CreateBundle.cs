using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Catalog.Application.Products;

public sealed record CreateBundle(Guid SellerId, string Sku, string Name, string Category, IReadOnlyList<BundleLine> Components)
    : ICommand<Guid>;
