using System.Globalization;

namespace Kanimambo.Promotions.Domain.Rules;

public sealed class LiteralNode(int position, RuleValue value) : RuleNode(position)
{
    public RuleValue Value { get; } = value;

    public override RuleType Type => Value.Type;

    public override RuleValue Evaluate(RuleContext context) => Value;

    public override string ToString() => Value.Type switch
    {
        RuleType.Number => Value.Number.ToString(CultureInfo.InvariantCulture),
        RuleType.Text => $"\"{Value.Text.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
        RuleType.Boolean => Value.Boolean ? "TRUE" : "FALSE",
        _ => $"[{string.Join(", ", Value.List.Select(v => $"\"{v}\""))}]",
    };
}
