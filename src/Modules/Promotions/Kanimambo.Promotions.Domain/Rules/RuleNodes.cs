using System.Globalization;

namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>The values of every variable for one evaluation (one cart, one moment).</summary>
public sealed class RuleContext(IReadOnlyDictionary<string, RuleValue> values)
{
    public RuleValue Get(string variable) =>
        values.TryGetValue(variable, out var value)
            ? value
            : throw new InvalidOperationException($"No value supplied for rule variable '{variable}'.");
}

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

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
}

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

public sealed class OrNode(int position, RuleNode left, RuleNode right) : RuleNode(position)
{
    public RuleNode Left { get; } = left;

    public RuleNode Right { get; } = right;

    public override RuleType Type => RuleType.Boolean;

    public override RuleValue Evaluate(RuleContext context) =>
        RuleValue.Of(Left.Evaluate(context).Boolean || Right.Evaluate(context).Boolean);

    public override string ToString() => $"{Left} OR {Right}";
}

public sealed class NotNode(int position, RuleNode operand) : RuleNode(position)
{
    public RuleNode Operand { get; } = operand;

    public override RuleType Type => RuleType.Boolean;

    public override RuleValue Evaluate(RuleContext context) => RuleValue.Of(!Operand.Evaluate(context).Boolean);

    public override string ToString() => Operand is LiteralNode or VariableNode ? $"NOT {Operand}" : $"NOT ({Operand})";
}
