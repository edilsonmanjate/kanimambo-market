using System.Linq.Expressions;

namespace Kanimambo.SharedKernel.Specifications;

internal sealed class NotSpecification<T>(Specification<T> inner) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = inner.ToExpression();
        return Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);
    }
}
