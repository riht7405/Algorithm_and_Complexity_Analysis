namespace LabApp.Core;

public static class PostfixEvaluator
{
    public static Result<double> Evaluate(IReadOnlyList<Token> tokens)
    {
        var stack = new LinkedListStack<double>();

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.Number:
                    stack.Push(token.Number!.Value);
                    break;

                case TokenKind.Plus:
                    if (stack.Count < 2) return Fail("недостаточно операндов для +");
                    { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a + b); }
                    break;
                case TokenKind.Minus:
                    if (stack.Count < 2) return Fail("недостаточно операндов для -");
                    { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a - b); }
                    break;
                case TokenKind.Mul:
                    if (stack.Count < 2) return Fail("недостаточно операндов для *");
                    { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a * b); }
                    break;
                case TokenKind.Div:
                    if (stack.Count < 2) return Fail("недостаточно операндов для /");
                    { var b = stack.Pop(); var a = stack.Pop();
                      if (b == 0) return Fail("деление на ноль");
                      stack.Push(a / b); }
                    break;
                case TokenKind.Pow:
                    if (stack.Count < 2) return Fail("недостаточно операндов для ^");
                    { var b = stack.Pop(); var a = stack.Pop(); stack.Push(Math.Pow(a, b)); }
                    break;

                case TokenKind.Ln:
                    if (stack.IsEmpty()) return Fail("недостаточно операндов для ln");
                    { var v = stack.Pop(); if (v <= 0) return Fail("ln от неположительного"); stack.Push(Math.Log(v)); }
                    break;
                case TokenKind.Sqrt:
                    if (stack.IsEmpty()) return Fail("недостаточно операндов для sqrt");
                    { var v = stack.Pop(); if (v < 0) return Fail("sqrt от отрицательного"); stack.Push(Math.Sqrt(v)); }
                    break;
                case TokenKind.Cos:
                    if (stack.IsEmpty()) return Fail("недостаточно операндов для cos");
                    stack.Push(Math.Cos(stack.Pop()));
                    break;
                case TokenKind.Sin:
                    if (stack.IsEmpty()) return Fail("недостаточно операндов для sin");
                    stack.Push(Math.Sin(stack.Pop()));
                    break;

                default:
                    return Fail($"неожиданный токен: {token.Lexeme}");
            }
        }

        if (stack.Count != 1) return Fail($"ожидался 1 результат, в стеке: {stack.Count}");
        return Result<double>.Ok(stack.Pop());
    }

    private static Result<double> Fail(string msg) => Result<double>.Fail(msg);
}