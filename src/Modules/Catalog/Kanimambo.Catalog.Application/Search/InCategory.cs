using System.Linq.Expressions;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Search;

public sealed class InCategory(string category) : Specification<CatalogEntry>
{
    private readonly string _category = CatalogItem.NormalizeCategory(category);

    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.Category == _category;
}
