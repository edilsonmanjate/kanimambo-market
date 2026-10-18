using System.Globalization;
using System.Linq.Expressions;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Search;

/// <summary>The seller delivers to the city (e.g. same-day delivery in Matola).</summary>
public sealed class DeliversTo(string city) : Specification<CatalogEntry>
{
    private readonly string _city = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(city.Trim().ToLowerInvariant());

    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.DeliveryCities.Contains(_city);
}
