using Kanimambo.Catalog.Application.Products;

namespace Kanimambo.Catalog.Api.Controllers;

public sealed record CreateBundleRequest(string Sku, string Name, string Category, IReadOnlyList<BundleLine> Components);
