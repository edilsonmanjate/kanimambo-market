using System.Linq.Expressions;

namespace Kanimambo.SharedKernel.Specifications;

internal sealed class TrueSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression() => _ => true;
}
