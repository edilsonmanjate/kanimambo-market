using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Catalog.Application.Products;

public sealed record GetProductDetails(Guid ProductId) : IQuery<ProductDetailsDto>;
