namespace Kanimambo.Promotions.Domain.Rules;

public sealed class OrNode(int position, RuleNode left, RuleNode right) : RuleNode(position)
{
    public RuleNode Left { get; } = left;

    public RuleNode Right { get; } = right;

    public override RuleType Type => RuleType.Boolean;

    public override RuleValue Evaluate(RuleContext context) =>
        RuleValue.Of(Left.Evaluate(context).Boolean || Right.Evaluate(context).Boolean);

    public override string ToString() => $"{Left} OR {Right}";
}
