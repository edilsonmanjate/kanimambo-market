namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>Thrown inside the lexer/parser; turned into a <see cref="RuleDiagnostic"/> at the boundary.</summary>
internal sealed class RuleSyntaxException(int position, int length, string message) : Exception(message)
{
    public int Position { get; } = position;

    public int Length { get; } = length;
}
