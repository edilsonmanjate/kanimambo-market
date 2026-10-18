using System.Linq.Expressions;

namespace Kanimambo.SharedKernel.Specifications;

internal sealed class OrSpecification<T>(Specification<T> left, Specification<T> right) : Specification<T>
{
    private readonly Specification<T> _right = right ?? throw new ArgumentNullException(nameof(right));

    public override Expression<Func<T, bool>> ToExpression() =>
        ExpressionComposer.Combine(left.ToExpression(), _right.ToExpression(), Expression.OrElse);
}
