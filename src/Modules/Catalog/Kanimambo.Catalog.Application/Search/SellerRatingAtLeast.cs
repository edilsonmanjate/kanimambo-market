using System.Linq.Expressions;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Search;

public sealed class SellerRatingAtLeast(decimal minimum) : Specification<CatalogEntry>
{
    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.SellerRating >= minimum;
}
