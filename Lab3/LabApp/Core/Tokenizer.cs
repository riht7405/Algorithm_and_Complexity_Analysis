using System.Globalization;

namespace LabApp.Core;

public static class Tokenizer
{
    public static Result<IReadOnlyList<Token>> Tokenize(string input)
    {
        if (input is null) return Fail("input is null");

        var tokens = new List<Token>();
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.')) i++;
                var lex = input[start..i];
                if (!double.TryParse(lex, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    return Fail($"некорректное число: {lex}");
                tokens.Add(new Token(TokenKind.Number, lex, n));
                continue;
            }

            switch (c)
            {
                case '+': tokens.Add(new Token(TokenKind.Plus, "+")); i++; break;
                case '-': tokens.Add(new Token(TokenKind.Minus, "-")); i++; break;
                case '*': tokens.Add(new Token(TokenKind.Mul, "*")); i++; break;
                case '/':
                case ':': tokens.Add(new Token(TokenKind.Div, c.ToString())); i++; break;
                case '^': tokens.Add(new Token(TokenKind.Pow, "^")); i++; break;
                case '(': tokens.Add(new Token(TokenKind.LParen, "(")); i++; break;
                case ')': tokens.Add(new Token(TokenKind.RParen, ")")); i++; break;
                default:
                    if (TryReadFunction(input, ref i, out var fn)) { tokens.Add(fn); continue; }
                    return Fail($"неизвестный символ: {c}");
            }
        }
        return Result<IReadOnlyList<Token>>.Ok(tokens);
    }

    private static bool TryReadFunction(string input, ref int i, out Token token)
    {
        token = null!;
        if (i + 2 <= input.Length && input.AsSpan(i, 2).SequenceEqual("ln".AsSpan()))
        { token = new Token(TokenKind.Ln, "ln"); i += 2; return true; }
        if (i + 3 <= input.Length && input.AsSpan(i, 3).SequenceEqual("cos".AsSpan()))
        { token = new Token(TokenKind.Cos, "cos"); i += 3; return true; }
        if (i + 3 <= input.Length && input.AsSpan(i, 3).SequenceEqual("sin".AsSpan()))
        { token = new Token(TokenKind.Sin, "sin"); i += 3; return true; }
        if (i + 4 <= input.Length && input.AsSpan(i, 4).SequenceEqual("sqrt".AsSpan()))
        { token = new Token(TokenKind.Sqrt, "sqrt"); i += 4; return true; }
        return false;
    }

    private static Result<IReadOnlyList<Token>> Fail(string msg) =>
        Result<IReadOnlyList<Token>>.Fail(msg);
}