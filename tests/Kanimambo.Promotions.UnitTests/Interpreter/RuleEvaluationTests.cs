using Kanimambo.Promotions.Domain.Rules;
using static Kanimambo.Promotions.UnitTests.Interpreter.RuleTestContext;

namespace Kanimambo.Promotions.UnitTests.Interpreter;

public sealed class RuleEvaluationTests
{
    private const string MPesaInMaputo = "cart.total >= 2500 AND customer.city = \"Maputo\" AND payment.method = \"MPesa\"";

    [Theory]
    [InlineData(2_500, "Maputo", "MPesa", true)]
    [InlineData(2_499.99, "Maputo", "MPesa", false)]
    [InlineData(3_000, "Matola", "MPesa", false)]
    [InlineData(3_000, "Maputo", "EMola", false)]
    public void The_marketing_example_rule_is_evaluated_at_runtime(decimal total, string city, string payment, bool expected)
    {
        Evaluate(MPesaInMaputo, With(total: total, city: city, payment: payment)).ShouldBe(expected);
    }

    [Fact]
    public void And_binds_tighter_than_or()
    {
        // Parsed as: (b2b AND Matola) OR total > 10000
        const string rule = "customer.type = \"b2b\" AND customer.city = \"Matola\" OR cart.total > 10000";

        Evaluate(rule, With(customerType: "guest", city: "Beira", total: 20_000m)).ShouldBeTrue();
        Evaluate(rule, With(customerType: "b2b", city: "Beira", total: 100m)).ShouldBeFalse();
        RuleParser.Parse(rule).Value.ToString().ShouldBe("customer.type = \"b2b\" AND customer.city = \"Matola\" OR cart.total > 10000");
    }

    [Fact]
    public void Parentheses_override_precedence()
    {
        const string rule = "customer.type = \"b2b\" AND (customer.city = \"Matola\" OR cart.total > 10000)";

        Evaluate(rule, With(customerType: "guest", city: "Beira", total: 20_000m)).ShouldBeFalse();
        Evaluate(rule, With(customerType: "b2b", city: "Beira", total: 20_000m)).ShouldBeTrue();
        RuleParser.Parse(rule).Value.ToString().ShouldBe(rule);
    }

    [Fact]
    public void In_works_with_literal_lists_and_list_variables()
    {
        Evaluate("customer.city IN [\"Maputo\", \"Matola\"]", With(city: "Matola")).ShouldBeTrue();
        Evaluate("customer.city IN [\"Maputo\", \"Matola\"]", With(city: "Nampula")).ShouldBeFalse();
        Evaluate("\"cabazes\" IN cart.categories", With(categories: ["mercearia", "cabazes"])).ShouldBeTrue();
    }

    [Fact]
    public void Not_and_booleans_compose()
    {
        Evaluate("NOT (payment.method = \"CashOnDelivery\")", With(payment: "MPesa")).ShouldBeTrue();
        Evaluate("NOT customer.city = \"Maputo\" OR TRUE", With()).ShouldBeTrue();
        Evaluate("FALSE", With()).ShouldBeFalse();
    }

    [Fact]
    public void Keywords_variables_and_text_comparisons_are_case_insensitive()
    {
        Evaluate("Cart.Total >= 100 and customer.city = \"MAPUTO\" or false", With(city: "maputo")).ShouldBeTrue();
    }

    [Fact]
    public void Numbers_support_decimals_and_every_comparison()
    {
        var context = With(total: 2_500.50m, items: 12);

        Evaluate("cart.total > 2500.49", context).ShouldBeTrue();
        Evaluate("cart.total < 2500.5", context).ShouldBeFalse();
        Evaluate("cart.total <= 2500.5 AND cart.itemCount != 11", context).ShouldBeTrue();
        Evaluate("cart.itemCount <> 12", context).ShouldBeFalse();
    }

    [Fact]
    public void Weekday_rules_read_the_evaluation_context()
    {
        Evaluate("today.weekday = \"Friday\" AND cart.total >= 500", With(weekday: "Friday")).ShouldBeTrue();
        Evaluate("today.weekday = \"Friday\" AND cart.total >= 500", With(weekday: "Monday")).ShouldBeFalse();
    }

    [Fact]
    public void A_compiled_rule_is_reusable_across_many_evaluations()
    {
        var rule = RuleParser.Parse(MPesaInMaputo).Value;

        Enumerable.Range(0, 1_000)
            .Count(i => rule.Evaluate(With(total: i * 5m, payment: "MPesa")))
            .ShouldBe(500); // totals 2 500 … 4 995
    }
}
