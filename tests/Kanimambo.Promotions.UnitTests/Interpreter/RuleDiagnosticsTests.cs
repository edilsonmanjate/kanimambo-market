using Kanimambo.Promotions.Domain.Rules;

namespace Kanimambo.Promotions.UnitTests.Interpreter;

/// <summary>Authoring errors are caught when the rule is saved, with the position of the problem.</summary>
public sealed class RuleDiagnosticsTests
{
    [Theory]
    [InlineData("cart.total >= 2500 AN customer.city = \"Maputo\"", 20, "Unexpected 'AN': expected AND, OR or end of rule.")]
    [InlineData("cart.total >=", 14, "Unexpected end of rule: expected a value, a variable or '('.")]
    [InlineData("custmer.city = \"Maputo\"", 1, "Unknown variable 'custmer.city'. Did you mean 'customer.city'?")]
    [InlineData("customer.city = \"Maputo", 17, "Text is missing its closing quote (\").")]
    [InlineData("cart.total = \"Maputo\"", 12, "Cannot compare cart.total (a number) with \"Maputo\" (text).")]
    [InlineData("customer.city > \"Maputo\"", 15, "'>' only works with numbers; customer.city is text.")]
    [InlineData("cart.total > 100 && TRUE", 18, "Use AND instead of '&&'.")]
    [InlineData("cart.total", 1, "The rule must be a condition (true/false), but it is a number.")]
    [InlineData("customer.city IN \"Maputo\"", 18, "The right side of IN must be a list, e.g. [\"Maputo\", \"Matola\"], but \"Maputo\" is text.")]
    [InlineData("(cart.total > 1", 16, "Unexpected end of rule: expected ')'.")]
    [InlineData("customer.city IN [\"Maputo\" \"Matola\"]", 28, "Unexpected \"Matola\": expected ',' or ']'.")]
    [InlineData("cart.total > 1.2.3", 14, "'1.2.3' is not a valid number.")]
    [InlineData("cart.total AND TRUE", 1, "AND needs conditions on both sides, but cart.total is a number.")]
    [InlineData("   ", 1, "The rule is empty.")]
    public void Each_mistake_gets_a_precise_position_and_message(string rule, int position, string message)
    {
        var (compiled, diagnostic) = RuleParser.Diagnose(rule);

        compiled.ShouldBeNull();
        diagnostic.ShouldNotBeNull();
        diagnostic.Position.ShouldBe(position);
        diagnostic.Message.ShouldBe(message);
    }

    [Fact]
    public void The_excerpt_points_at_the_offending_token()
    {
        const string rule = "cart.total >= 2500 AN customer.city = \"Maputo\"";

        var diagnostic = RuleParser.Diagnose(rule).Diagnostic!;

        diagnostic.Excerpt(rule).ShouldBe(rule + "\n" + new string(' ', 19) + "^^");
    }

    [Fact]
    public void Parse_reports_the_position_inside_a_validation_error()
    {
        var error = RuleParser.Parse("cart.total >= 2500 AN customer.city = \"Maputo\"").Error;

        error.ShouldBeOfType<SharedKernel.Results.ValidationError>()
            .Errors.ShouldHaveSingleItem().Message.ShouldStartWith("Position 20: Unexpected 'AN'");
    }

    [Fact]
    public void Unknown_variables_far_from_any_known_name_list_the_vocabulary()
    {
        var diagnostic = RuleParser.Diagnose("weather = \"sunny\"").Diagnostic!;

        diagnostic.Message.ShouldStartWith("Unknown variable 'weather'. Known variables: ");
        diagnostic.Message.ShouldContain(RuleVariables.PaymentMethod);
    }
}
