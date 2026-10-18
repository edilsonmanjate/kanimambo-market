using System.Linq.Expressions;

namespace Kanimambo.SharedKernel.Specifications;

/// <summary>
/// A business rule expressed as an <see cref="Expression"/> so that the same rule can be
/// evaluated in memory (<see cref="IsSatisfiedBy"/>) and translated to SQL by EF Core.
/// Combinations rebind parameters instead of using <c>Expression.Invoke</c>, which keeps
/// the composed expression translatable.
/// </summary>
public abstract class Specification<T>
{
    private Func<T, bool>? _compiled;

    public static Specification<T> All { get; } = new TrueSpecification<T>();

    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T candidate) => (_compiled ??= ToExpression().Compile())(candidate);

    public Specification<T> And(Specification<T> other) => new AndSpecification<T>(this, other);

    public Specification<T> Or(Specification<T> other) => new OrSpecification<T>(this, other);

    public Specification<T> Not() => new NotSpecification<T>(this);

    public Specification<T> BitwiseAnd(Specification<T> other) => And(other);

    public Specification<T> BitwiseOr(Specification<T> other) => Or(other);

    public Specification<T> LogicalNot() => Not();

    public static Specification<T> operator &(Specification<T> left, Specification<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.And(right);
    }

    public static Specification<T> operator |(Specification<T> left, Specification<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.Or(right);
    }

    public static Specification<T> operator !(Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return specification.Not();
    }
}
