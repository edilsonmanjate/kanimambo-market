namespace Kanimambo.Promotions.Domain.Rules;

public static class RuleTypeNames
{
    public static string Describe(this RuleType type) => type switch
    {
        RuleType.Number => "a number",
        RuleType.Text => "text",
        RuleType.Boolean => "a condition (true/false)",
        RuleType.TextList => "a list of text",
        _ => type.ToString(),
    };
}
