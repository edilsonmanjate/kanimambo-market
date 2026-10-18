namespace Kanimambo.Promotions.Domain.Rules;

internal enum TokenKind
{
    Number,
    Text,
    Identifier,
    And,
    Or,
    Not,
    In,
    True,
    False,
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Comma,
    End,
}
