namespace LabApp.Core;

public enum TokenKind
{
    Number,
    Plus,
    Minus,
    Mul,
    Div,
    Pow,
    Ln,
    Cos,
    Sin,
    Sqrt,
    LParen,
    RParen
}

public sealed record Token(TokenKind Kind, string Lexeme, double? Number = null);