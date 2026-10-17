namespace Kanimambo.SharedKernel.Specifications;

public static class SpecificationExtensions
{
    /// <summary>Filters a query with a specification; EF Core translates it to SQL.</summary>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return Queryable.Where(source, specification.ToExpression());
    }
}
