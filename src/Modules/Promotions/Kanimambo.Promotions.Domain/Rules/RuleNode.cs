namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>
/// Interpreter pattern: the abstract syntax tree interprets itself. Each node knows its static type
/// (checked by the parser) and how to evaluate itself against a <see cref="RuleContext"/>.
/// </summary>
public abstract class RuleNode(int position)
{
    public int Position { get; } = position;

    public abstract RuleType Type { get; }

    public abstract RuleValue Evaluate(RuleContext context);

    /// <summary>Canonical text of the expression (normalized keywords, spacing and parentheses).</summary>
    public abstract override string ToString();
}
