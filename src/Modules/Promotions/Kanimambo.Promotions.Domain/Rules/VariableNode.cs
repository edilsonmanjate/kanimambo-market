namespace Kanimambo.Promotions.Domain.Rules;

public sealed class VariableNode(int position, RuleVariable variable) : RuleNode(position)
{
    public RuleVariable Variable { get; } = variable;

    public override RuleType Type => Variable.Type;

    public override RuleValue Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(Variable.Name);
    }

    public override string ToString() => Variable.Name;
}
