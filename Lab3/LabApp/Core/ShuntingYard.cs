namespace LabApp.Core;

public static class ShuntingYard
{
    public static Result<IReadOnlyList<Token>> ToPostfix(IReadOnlyList<Token> tokens)
    {
        var output = new List<Token>();
        var ops = new LinkedListStack<Token>();

        foreach (var t in tokens)
        {
            switch (t.Kind)
            {
                case TokenKind.Number:
                    output.Add(t);
                    break;

                case TokenKind.Ln:
                case TokenKind.Cos:
                case TokenKind.Sin:
                case TokenKind.Sqrt:
                    ops.Push(t);
                    break;

                case TokenKind.Plus:
                case TokenKind.Minus:
                case TokenKind.Mul:
                case TokenKind.Div:
                case TokenKind.Pow:
                    while (ops.Count > 0 && IsOperator(ops.Top()) &&
                           (Precedence(ops.Top()) > Precedence(t) ||
                            (Precedence(ops.Top()) == Precedence(t) && !IsRightAssoc(t))))
                    {
                        output.Add(ops.Pop());
                    }
                    ops.Push(t);
                    break;

                case TokenKind.LParen:
                    ops.Push(t);
                    break;

                case TokenKind.RParen:
                {
                    bool foundLeft = false;
                    while (ops.Count > 0)
                    {
                        var top = ops.Pop();
                        if (top.Kind == TokenKind.LParen) { foundLeft = true; break; }
                        output.Add(top);
                    }
                    if (!foundLeft) return Fail("несбалансированные скобки");
                    if (ops.Count > 0 && IsFunction(ops.Top()))
                        output.Add(ops.Pop());
                    break;
                }

                default:
                    return Fail($"неожиданный токен: {t.Lexeme}");
            }
        }

        while (ops.Count > 0)
        {
            var top = ops.Pop();
            if (top.Kind == TokenKind.LParen) return Fail("несбалансированные скобки");
            output.Add(top);
        }

        return Result<IReadOnlyList<Token>>.Ok(output);
    }

    private static int Precedence(Token t) => t.Kind switch
    {
        TokenKind.Plus or TokenKind.Minus => 1,
        TokenKind.Mul or TokenKind.Div => 2,
        TokenKind.Pow => 3,
        TokenKind.Ln or TokenKind.Cos or TokenKind.Sin or TokenKind.Sqrt => 4,
        _ => 0
    };

    private static bool IsRightAssoc(Token t) => t.Kind == TokenKind.Pow;
    private static bool IsOperator(Token t) => t.Kind is TokenKind.Plus or TokenKind.Minus
        or TokenKind.Mul or TokenKind.Div or TokenKind.Pow;
    private static bool IsFunction(Token t) => t.Kind is TokenKind.Ln or TokenKind.Cos
        or TokenKind.Sin or TokenKind.Sqrt;

    private static Result<IReadOnlyList<Token>> Fail(string msg) =>
        Result<IReadOnlyList<Token>>.Fail(msg);
}