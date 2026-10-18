namespace Kanimambo.Promotions.Domain.Rules;

public sealed class AndNode(int position, RuleNode left, RuleNode right) : RuleNode(position)
{
    public RuleNode Left { get; } = left;

    public RuleNode Right { get; } = right;

    public override RuleType Type => RuleType.Boolean;

    // Short-circuits like the reader expects.
    public override RuleValue Evaluate(RuleContext context) =>
        RuleValue.Of(Left.Evaluate(context).Boolean && Right.Evaluate(context).Boolean);

    public override string ToString() => $"{Wrap(Left)} AND {Wrap(Right)}";

    private static string Wrap(RuleNode node) => node is OrNode ? $"({node})" : node.ToString();
}
