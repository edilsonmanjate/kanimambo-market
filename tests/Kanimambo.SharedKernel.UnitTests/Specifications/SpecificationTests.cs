using System.Linq.Expressions;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.SharedKernel.UnitTests.Specifications;

public sealed class SpecificationTests
{
    private static readonly Listing Rice = new("Arroz 25 kg", 1_850m, Stock: 12, City: "Maputo");
    private static readonly Listing Oil = new("Óleo 5 L", 720m, Stock: 0, City: "Matola");
    private static readonly Listing Cashew = new("Castanha de caju 1 kg", 950m, Stock: 30, City: "Nampula");

    private static readonly Listing[] Catalog = [Rice, Oil, Cashew];

    [Fact]
    public void And_requires_both_rules()
    {
        var spec = new InStock().And(new PriceBetween(500m, 1_000m));

        Catalog.Where(spec.IsSatisfiedBy).ShouldBe([Cashew]);
    }

    [Fact]
    public void Or_accepts_either_rule()
    {
        var spec = new InCity("Matola") | new InCity("Nampula");

        Catalog.Where(spec.IsSatisfiedBy).ShouldBe([Oil, Cashew]);
    }

    [Fact]
    public void Not_negates_the_rule()
    {
        Catalog.Where((!new InStock()).IsSatisfiedBy).ShouldBe([Oil]);
    }

    [Fact]
    public void All_matches_everything_and_is_a_neutral_seed_for_composition()
    {
        var spec = Specification<Listing>.All & new InCity("Maputo");

        Catalog.Where(spec.IsSatisfiedBy).ShouldBe([Rice]);
    }

    [Fact]
    public void Composed_expression_has_a_single_parameter_and_no_invocations_so_ef_can_translate_it()
    {
        var spec = (new InStock() & new PriceBetween(500m, 2_000m)) | !new InCity("Beira");

        var expression = spec.ToExpression();

        expression.Parameters.Count.ShouldBe(1);
        var inspector = new ExpressionInspector();
        inspector.Visit(expression.Body);
        inspector.Invocations.ShouldBe(0);
        inspector.Parameters.ShouldBe([expression.Parameters[0]]);
    }

    [Fact]
    public void Specifications_filter_iqueryable_sources_through_the_expression_tree()
    {
        var query = Catalog.AsQueryable().Where(new InStock() & new InCity("Nampula"));

        query.Expression.ToString().ShouldContain("Stock");
        query.ToList().ShouldBe([Cashew]);
    }

    private sealed record Listing(string Name, decimal Price, int Stock, string City);

    private sealed class InStock : Specification<Listing>
    {
        public override Expression<Func<Listing, bool>> ToExpression() => l => l.Stock > 0;
    }

    private sealed class PriceBetween(decimal min, decimal max) : Specification<Listing>
    {
        public override Expression<Func<Listing, bool>> ToExpression() => l => l.Price >= min && l.Price <= max;
    }

    private sealed class InCity(string city) : Specification<Listing>
    {
        public override Expression<Func<Listing, bool>> ToExpression() => l => l.City == city;
    }

    private sealed class ExpressionInspector : ExpressionVisitor
    {
        public int Invocations { get; private set; }

        public HashSet<ParameterExpression> Parameters { get; } = [];

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            Invocations++;
            return base.VisitInvocation(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Parameters.Add(node);
            return base.VisitParameter(node);
        }
    }
}
