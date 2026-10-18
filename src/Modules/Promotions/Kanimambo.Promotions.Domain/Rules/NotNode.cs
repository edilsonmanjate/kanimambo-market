namespace Kanimambo.Promotions.Domain.Rules;

public sealed class NotNode(int position, RuleNode operand) : RuleNode(position)
{
    public RuleNode Operand { get; } = operand;

    public override RuleType Type => RuleType.Boolean;

    public override RuleValue Evaluate(RuleContext context) => RuleValue.Of(!Operand.Evaluate(context).Boolean);

    public override string ToString() => Operand is LiteralNode or VariableNode ? $"NOT {Operand}" : $"NOT ({Operand})";
}
