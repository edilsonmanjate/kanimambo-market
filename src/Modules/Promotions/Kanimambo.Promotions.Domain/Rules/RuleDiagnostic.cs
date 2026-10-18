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
