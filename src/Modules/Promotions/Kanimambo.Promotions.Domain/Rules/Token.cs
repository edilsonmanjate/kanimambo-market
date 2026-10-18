using System.Globalization;

namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>A token with its 0-based position in the rule text.</summary>
internal sealed record Token(TokenKind Kind, string Text, int Position, int Length)
{
    public decimal Number => decimal.Parse(Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public string Describe() => Kind switch
    {
        TokenKind.End => "end of rule",
        TokenKind.Text => $"\"{Text}\"",
        _ => $"'{Text}'",
    };
}
