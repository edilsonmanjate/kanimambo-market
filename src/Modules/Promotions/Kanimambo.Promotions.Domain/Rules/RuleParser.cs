using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>A precise authoring error: 1-based position, length of the offending text, and a human message.</summary>
public sealed record RuleDiagnostic(int Position, int Length, string Message)
{
    /// <summary>The rule with a caret line pointing at the problem, for display in the back-office.</summary>
    public string Excerpt(string rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return $"{rule}\n{new string(' ', Position - 1)}{new string('^', Math.Max(1, Length))}";
    }

    public override string ToString() => $"Position {Position}: {Message}";
}

/// <summary>A parsed, type-checked rule ready to be evaluated many times.</summary>
public sealed class CompiledRule
{
    internal CompiledRule(RuleNode root) => Root = root;

    public RuleNode Root { get; }

    public bool Evaluate(RuleContext context) => Root.Evaluate(context).Boolean;

    public override string ToString() => Root.ToString();
}

/// <summary>
/// Recursive-descent parser with type checking. Grammar (lowest precedence first):
/// <code>
/// rule       := or END
/// or         := and ( OR and )*
/// and        := not ( AND not )*
/// not        := NOT not | comparison
/// comparison := operand ( ( = | != | &gt; | &gt;= | &lt; | &lt;= ) operand | IN operand )?
/// operand    := NUMBER | "TEXT" | TRUE | FALSE | variable | ( or ) | [ "TEXT" ( , "TEXT" )* ]
/// </code>
/// </summary>
public sealed class RuleParser
{
    private readonly List<Token> _tokens;
    private int _index;

    private RuleParser(List<Token> tokens) => _tokens = tokens;

    private Token Current => _tokens[_index];

    public static Result<CompiledRule> Parse(string? rule)
    {
        var outcome = Diagnose(rule);
        return outcome.Rule is { } compiled
            ? compiled
            : Error.Validation(new FieldError("rule", outcome.Diagnostic!.ToString()));
    }

    /// <summary>Parses and returns either the compiled rule or the first diagnostic (never both).</summary>
    public static (CompiledRule? Rule, RuleDiagnostic? Diagnostic) Diagnose(string? rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            return (null, new RuleDiagnostic(1, 0, "The rule is empty."));
        }

        try
        {
            var parser = new RuleParser(RuleLexer.Tokenize(rule));
            var root = parser.ParseOr();
            parser.Expect(TokenKind.End, "AND, OR or end of rule");

            if (root.Type != RuleType.Boolean)
            {
                throw new RuleSyntaxException(0, rule.Length, $"The rule must be a condition (true/false), but it is {root.Type.Describe()}.");
            }

            return (new CompiledRule(root), null);
        }
        catch (RuleSyntaxException exception)
        {
            return (null, new RuleDiagnostic(exception.Position + 1, exception.Length, exception.Message));
        }
    }

    private RuleNode ParseOr()
    {
        var left = ParseAnd();
        while (Current.Kind == TokenKind.Or)
        {
            var op = Advance();
            var right = ParseAnd();
            left = new OrNode(op.Position, RequireCondition(left, op), RequireCondition(right, op));
        }

        return left;
    }

    private RuleNode ParseAnd()
    {
        var left = ParseNot();
        while (Current.Kind == TokenKind.And)
        {
            var op = Advance();
            var right = ParseNot();
            left = new AndNode(op.Position, RequireCondition(left, op), RequireCondition(right, op));
        }

        return left;
    }

    private RuleNode ParseNot()
    {
        if (Current.Kind != TokenKind.Not)
        {
            return ParseComparison();
        }

        var op = Advance();
        return new NotNode(op.Position, RequireCondition(ParseNot(), op));
    }

    private RuleNode ParseComparison()
    {
        var left = ParseOperand();

        if (Current.Kind == TokenKind.In)
        {
            var op = Advance();
            var list = ParseOperand();
            RequireType(left, RuleType.Text, $"The left side of IN must be text, but {left} is {left.Type.Describe()}.");
            RequireType(list, RuleType.TextList, $"The right side of IN must be a list, e.g. [\"Maputo\", \"Matola\"], but {list} is {list.Type.Describe()}.");
            return new InNode(op.Position, left, list);
        }

        if (ComparisonOf(Current.Kind) is not { } comparison)
        {
            return left;
        }

        var opToken = Advance();
        var right = ParseOperand();

        if (left.Type != right.Type)
        {
            throw new RuleSyntaxException(opToken.Position, opToken.Length,
                $"Cannot compare {left} ({left.Type.Describe()}) with {right} ({right.Type.Describe()}).");
        }

        var ordering = comparison is not (ComparisonOperator.Equal or ComparisonOperator.NotEqual);
        if (left.Type == RuleType.TextList || (ordering && left.Type != RuleType.Number))
        {
            throw new RuleSyntaxException(opToken.Position, opToken.Length,
                $"'{opToken.Text}' only works with numbers; {left} is {left.Type.Describe()}.");
        }

        return new ComparisonNode(opToken.Position, comparison, left, right);
    }

    private RuleNode ParseOperand()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                Advance();
                return new LiteralNode(token.Position, RuleValue.Of(token.Number));
            case TokenKind.Text:
                Advance();
                return new LiteralNode(token.Position, RuleValue.Of(token.Text));
            case TokenKind.True or TokenKind.False:
                Advance();
                return new LiteralNode(token.Position, RuleValue.Of(token.Kind == TokenKind.True));
            case TokenKind.Identifier:
                Advance();
                return Variable(token);
            case TokenKind.LeftParen:
                Advance();
                var inner = ParseOr();
                Expect(TokenKind.RightParen, "')'");
                return inner;
            case TokenKind.LeftBracket:
                return ParseList();
            default:
                throw new RuleSyntaxException(token.Position, Math.Max(1, token.Length),
                    $"Unexpected {token.Describe()}: expected a value, a variable or '('.");
        }
    }

    private LiteralNode ParseList()
    {
        var open = Advance();
        var items = new List<string>();
        do
        {
            if (Current.Kind != TokenKind.Text)
            {
                throw new RuleSyntaxException(Current.Position, Math.Max(1, Current.Length),
                    $"Unexpected {Current.Describe()}: list items must be text in quotes, e.g. [\"Maputo\", \"Matola\"].");
            }

            items.Add(Advance().Text);
        }
        while (Current.Kind == TokenKind.Comma && Advance() is not null);

        Expect(TokenKind.RightBracket, "',' or ']'");
        return new LiteralNode(open.Position, RuleValue.Of(items));
    }

    private static VariableNode Variable(Token token)
    {
        if (RuleVariables.Find(token.Text) is { } variable)
        {
            return new VariableNode(token.Position, variable);
        }

        var suggestion = RuleVariables.Suggest(token.Text);
        throw new RuleSyntaxException(token.Position, token.Length, suggestion is null
            ? $"Unknown variable '{token.Text}'. Known variables: {string.Join(", ", RuleVariables.All.Select(v => v.Name))}."
            : $"Unknown variable '{token.Text}'. Did you mean '{suggestion}'?");
    }

    private static RuleNode RequireCondition(RuleNode node, Token op)
    {
        if (node.Type != RuleType.Boolean)
        {
            throw new RuleSyntaxException(node.Position, 1,
                $"{op.Text.ToUpperInvariant()} needs conditions on both sides, but {node} is {node.Type.Describe()}.");
        }

        return node;
    }

    private static void RequireType(RuleNode node, RuleType type, string message)
    {
        if (node.Type != type)
        {
            throw new RuleSyntaxException(node.Position, 1, message);
        }
    }

    private static ComparisonOperator? ComparisonOf(TokenKind kind) => kind switch
    {
        TokenKind.Equal => ComparisonOperator.Equal,
        TokenKind.NotEqual => ComparisonOperator.NotEqual,
        TokenKind.Greater => ComparisonOperator.Greater,
        TokenKind.GreaterOrEqual => ComparisonOperator.GreaterOrEqual,
        TokenKind.Less => ComparisonOperator.Less,
        TokenKind.LessOrEqual => ComparisonOperator.LessOrEqual,
        _ => null,
    };

    private Token Advance() => _tokens[_index++];

    private void Expect(TokenKind kind, string expected)
    {
        if (Current.Kind != kind)
        {
            throw new RuleSyntaxException(Current.Position, Math.Max(1, Current.Length), $"Unexpected {Current.Describe()}: expected {expected}.");
        }

        Advance();
    }
}
