using System.Text;

namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>Tokenizer: turns rule text into tokens. Keywords and identifiers are case-insensitive.</summary>
internal static class RuleLexer
{
    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AND"] = TokenKind.And,
        ["OR"] = TokenKind.Or,
        ["NOT"] = TokenKind.Not,
        ["IN"] = TokenKind.In,
        ["TRUE"] = TokenKind.True,
        ["FALSE"] = TokenKind.False,
    };

    public static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            var start = i;
            if (char.IsAsciiDigit(c))
            {
                while (i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] == '.'))
                {
                    i++;
                }

                var number = text[start..i];
                if (number.Count(ch => ch == '.') > 1 || number.EndsWith('.'))
                {
                    throw new RuleSyntaxException(start, i - start, $"'{number}' is not a valid number.");
                }

                tokens.Add(new Token(TokenKind.Number, number, start, i - start));
            }
            else if (char.IsAsciiLetter(c) || c == '_')
            {
                while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] is '_' or '.'))
                {
                    i++;
                }

                var word = text[start..i];
                tokens.Add(new Token(Keywords.GetValueOrDefault(word, TokenKind.Identifier), word, start, i - start));
            }
            else if (c == '"')
            {
                tokens.Add(ReadText(text, ref i));
            }
            else
            {
                tokens.Add(ReadSymbol(text, ref i));
            }
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, text.Length, 0));
        return tokens;
    }

    private static Token ReadText(string text, ref int i)
    {
        var start = i++;
        var value = new StringBuilder();
        while (i < text.Length && text[i] != '"')
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '"')
            {
                i++;
            }

            value.Append(text[i++]);
        }

        if (i >= text.Length)
        {
            throw new RuleSyntaxException(start, text.Length - start, "Text is missing its closing quote (\").");
        }

        i++;
        return new Token(TokenKind.Text, value.ToString(), start, i - start);
    }

    private static Token ReadSymbol(string text, ref int i)
    {
        var start = i;
        var next = i + 1 < text.Length ? text[i + 1] : '\0';

        (TokenKind Kind, int Length) symbol = (text[i], next) switch
        {
            ('>', '=') => (TokenKind.GreaterOrEqual, 2),
            ('<', '=') => (TokenKind.LessOrEqual, 2),
            ('!', '=') => (TokenKind.NotEqual, 2),
            ('<', '>') => (TokenKind.NotEqual, 2),
            ('=', '=') => (TokenKind.Equal, 2),
            ('=', _) => (TokenKind.Equal, 1),
            ('>', _) => (TokenKind.Greater, 1),
            ('<', _) => (TokenKind.Less, 1),
            ('(', _) => (TokenKind.LeftParen, 1),
            (')', _) => (TokenKind.RightParen, 1),
            ('[', _) => (TokenKind.LeftBracket, 1),
            (']', _) => (TokenKind.RightBracket, 1),
            (',', _) => (TokenKind.Comma, 1),
            ('&', '&') => throw new RuleSyntaxException(start, 2, "Use AND instead of '&&'."),
            ('|', '|') => throw new RuleSyntaxException(start, 2, "Use OR instead of '||'."),
            _ => throw new RuleSyntaxException(start, 1, $"Unexpected character '{text[i]}'."),
        };

        i += symbol.Length;
        return new Token(symbol.Kind, text.Substring(start, symbol.Length), start, symbol.Length);
    }
}
