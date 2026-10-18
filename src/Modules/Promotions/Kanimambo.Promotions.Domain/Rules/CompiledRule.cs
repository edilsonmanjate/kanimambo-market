namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>A parsed, type-checked rule ready to be evaluated many times.</summary>
public sealed class CompiledRule
{
    internal CompiledRule(RuleNode root) => Root = root;

    public RuleNode Root { get; }

    public bool Evaluate(RuleContext context) => Root.Evaluate(context).Boolean;

    public override string ToString() => Root.ToString();
}
