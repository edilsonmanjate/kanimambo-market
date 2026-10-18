using System.Linq.Expressions;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Search;

/// <summary>Has at least one unit available (for bundles: derived from the components' stock).</summary>
public sealed class InStock : Specification<CatalogEntry>
{
    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.AvailableStock > 0;
}
