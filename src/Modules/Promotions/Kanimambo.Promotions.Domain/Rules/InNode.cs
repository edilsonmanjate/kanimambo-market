namespace Kanimambo.Promotions.Domain.Rules;

/// <summary><c>text IN list</c>, case-insensitive.</summary>
public sealed class InNode(int position, RuleNode item, RuleNode list) : RuleNode(position)
{
    public RuleNode Item { get; } = item;

    public RuleNode List { get; } = list;

    public override RuleType Type => RuleType.Boolean;

    public override RuleValue Evaluate(RuleContext context) =>
        RuleValue.Of(List.Evaluate(context).List.Contains(Item.Evaluate(context).Text, StringComparer.OrdinalIgnoreCase));

    public override string ToString() => $"{Item} IN {List}";
}
