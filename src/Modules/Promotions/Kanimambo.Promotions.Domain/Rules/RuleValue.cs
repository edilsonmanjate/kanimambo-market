namespace Kanimambo.Promotions.Domain.Rules;

public enum RuleType
{
    Number,
    Text,
    Boolean,
    TextList,
}

/// <summary>A typed value flowing through the rule interpreter.</summary>
public readonly record struct RuleValue
{
    private RuleValue(RuleType type, decimal number = 0m, string text = "", bool boolean = false, IReadOnlyList<string>? list = null)
    {
        Type = type;
        Number = number;
        Text = text;
        Boolean = boolean;
        List = list ?? [];
    }

    public RuleType Type { get; }

    public decimal Number { get; }

    public string Text { get; }

    public bool Boolean { get; }

    public IReadOnlyList<string> List { get; }

    public static RuleValue Of(decimal number) => new(RuleType.Number, number: number);

    public static RuleValue Of(string text) => new(RuleType.Text, text: text ?? string.Empty);

    public static RuleValue Of(bool boolean) => new(RuleType.Boolean, boolean: boolean);

    public static RuleValue Of(IReadOnlyList<string> list) => new(RuleType.TextList, list: list);
}

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
