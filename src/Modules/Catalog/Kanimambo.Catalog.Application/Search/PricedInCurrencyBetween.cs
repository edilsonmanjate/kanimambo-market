using System.Linq.Expressions;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Search;

public sealed class PricedInCurrencyBetween(Currency currency, decimal? minimum, decimal? maximum) : Specification<CatalogEntry>
{
    private readonly string _currency = currency.Code;
    private readonly decimal _minimum = minimum ?? decimal.MinValue;
    private readonly decimal _maximum = maximum ?? decimal.MaxValue;

    public override Expression<Func<CatalogEntry, bool>> ToExpression() =>
        e => e.Currency == _currency && e.Price >= _minimum && e.Price <= _maximum;
}
