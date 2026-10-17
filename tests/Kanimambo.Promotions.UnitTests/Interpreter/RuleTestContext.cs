using Kanimambo.Promotions.Domain.Rules;

namespace Kanimambo.Promotions.UnitTests.Interpreter;

/// <summary>Builds evaluation contexts with sensible defaults, overriding only what a test cares about.</summary>
internal static class RuleTestContext
{
    public static RuleContext With(
        decimal total = 1_000m,
        int items = 3,
        string[]? categories = null,
        string city = "Maputo",
        string customerType = "guest",
        string payment = "None",
        string weekday = "Friday") =>
        new(new Dictionary<string, RuleValue>
        {
            [RuleVariables.CartTotal] = RuleValue.Of(total),
            [RuleVariables.CartItemCount] = RuleValue.Of(items),
            [RuleVariables.CartCategories] = RuleValue.Of(categories ?? ["mercearia"]),
            [RuleVariables.CustomerCity] = RuleValue.Of(city),
            [RuleVariables.CustomerType] = RuleValue.Of(customerType),
            [RuleVariables.PaymentMethod] = RuleValue.Of(payment),
            [RuleVariables.TodayWeekday] = RuleValue.Of(weekday),
        });

    public static bool Evaluate(string rule, RuleContext context) => RuleParser.Parse(rule).Value.Evaluate(context);
}
