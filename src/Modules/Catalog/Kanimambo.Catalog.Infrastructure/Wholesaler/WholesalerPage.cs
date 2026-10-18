using Kanimambo.Catalog.Application.Abstractions;

namespace Kanimambo.Catalog.Infrastructure.Wholesaler;

internal sealed record WholesalerPage(int Page, int PageSize, int TotalPages, IReadOnlyList<WholesalerProduct> Items, int? NextPage);
