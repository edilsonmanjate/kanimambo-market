namespace Kanimambo.Promotions.Domain.Rules;

public sealed class ComparisonNode(int position, ComparisonOperator op, RuleNode left, RuleNode right) : RuleNode(position)
{
    public ComparisonOperator Operator { get; } = op;

    public RuleNode Left { get; } = left;

    public RuleNode Right { get; } = right;

    public override RuleType Type => RuleType.Boolean;

    public override RuleValue Evaluate(RuleContext context)
    {
        var l = Left.Evaluate(context);
        var r = Right.Evaluate(context);

        var order = l.Type switch
        {
            RuleType.Number => l.Number.CompareTo(r.Number),
            RuleType.Text => string.Compare(l.Text, r.Text, StringComparison.OrdinalIgnoreCase),
            RuleType.Boolean => l.Boolean.CompareTo(r.Boolean),
            _ => throw new InvalidOperationException("Lists cannot be compared."),
        };

        return RuleValue.Of(Operator switch
        {
            ComparisonOperator.Equal => order == 0,
            ComparisonOperator.NotEqual => order != 0,
            ComparisonOperator.Greater => order > 0,
            ComparisonOperator.GreaterOrEqual => order >= 0,
            ComparisonOperator.Less => order < 0,
            _ => order <= 0,
        });
    }

    public override string ToString() => $"{Left} {Symbol} {Right}";

    private string Symbol => Operator switch
    {
        ComparisonOperator.Equal => "=",
        ComparisonOperator.NotEqual => "!=",
        ComparisonOperator.Greater => ">",
        ComparisonOperator.GreaterOrEqual => ">=",
        ComparisonOperator.Less => "<",
        _ => "<=",
    };
}
