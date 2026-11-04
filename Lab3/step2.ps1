<#
.SYNOPSIS
    Шаг 2: бизнес-логика LabApp (парсер, сценарии, выражения, бенчмарки).
.DESCRIPTION
    Добавляет файлы в LabApp/Core и тесты в Tests/LabApp.Tests.
    Не трогает существующие структуры (Stack.cs, Queue.cs, SinglyLinkedList.cs).
    Использует .slnx, .NET 10, UTF-8 with BOM. Без git.
.PARAMETER Root
    Рабочая папка. По умолчанию — текущая.
#>
[CmdletBinding()]
param([string]$Root = ".")

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}
function Write-Info([string]$msg) {
    Write-Host "  $msg"
}
function Write-File([string]$path, [string]$content) {
    $full = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $path))
    $dir = [System.IO.Path]::GetDirectoryName($full)
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($full, $content, [System.Text.UTF8Encoding]::new($true))
    Write-Info "+ $path"
}

# ---------- 0. Проверка окружения ----------
Write-Step "Проверка окружения"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK не найден"
}
$rootPath = (Resolve-Path $Root).Path
Set-Location $rootPath
Write-Info "папка: $rootPath"

if (-not (Test-Path "LabApp/LabApp.csproj")) {
    throw "Не найден LabApp/LabApp.csproj. Сначала выполни Шаг 1 (scaffold.ps1)."
}

$sln = $null
if (Test-Path "LabApp.slnx")    { $sln = "LabApp.slnx" }
elseif (Test-Path "LabApp.sln") { $sln = "LabApp.sln" }
else { throw "Не найден solution-файл. Сначала выполни Шаг 1." }
Write-Info "solution: $sln"

# ---------- 1. Core: базовые типы ----------
Write-Step "Core: базовые типы"

Write-File "LabApp/Core/Result.cs" @'
namespace LabApp.Core;

public sealed class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    private Result(bool ok, string? error) { IsSuccess = ok; Error = error; }
    public static Result Ok() => new(true, null);
    public static Result Fail(string error) => new(false, error);
}

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string? Error { get; }
    private Result(bool ok, T value, string? error) { IsSuccess = ok; Value = value; Error = error; }
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default!, error);
}
'@

Write-File "LabApp/Core/ScalarValue.cs" @'
using System.Globalization;

namespace LabApp.Core;

public sealed record ScalarValue(string Raw, bool IsNumber)
{
    public static bool TryParse(string token, out ScalarValue value)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            value = null!;
            return false;
        }
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            value = new ScalarValue(token, true);
            return true;
        }
        value = new ScalarValue(token, false);
        return true;
    }

    public double AsNumber() => double.Parse(Raw, CultureInfo.InvariantCulture);
    public override string ToString() => Raw;
}
'@

Write-File "LabApp/Core/Command.cs" @'
namespace LabApp.Core;

public enum CommandKind
{
    PushOrEnqueue = 1,
    PopOrDequeue = 2,
    TopOrPeek = 3,
    IsEmpty = 4,
    Print = 5
}

public sealed record Command(CommandKind Kind, ScalarValue? Argument = null);
'@

Write-File "LabApp/Core/TraceEvent.cs" @'
namespace LabApp.Core;

public sealed record TraceEvent(int Index, string Operation, string? Argument, string Result);
'@

Write-File "LabApp/Core/Timing.cs" @'
namespace LabApp.Core;

public sealed class Timing
{
    public double ReadMs { get; set; }
    public double ParseMs { get; set; }
    public double ExecuteMs { get; set; }
    public double TotalMs => ReadMs + ParseMs + ExecuteMs;
    public override string ToString() =>
        $"read={ReadMs:F3}ms, parse={ParseMs:F3}ms, execute={ExecuteMs:F3}ms, total={TotalMs:F3}ms";
}
'@

Write-File "LabApp/Core/ScenarioResult.cs" @'
namespace LabApp.Core;

public sealed class ScenarioResult
{
    public IReadOnlyList<TraceEvent> Trace { get; init; } = Array.Empty<TraceEvent>();
    public string FinalState { get; init; } = "[]";
    public Timing Timing { get; init; } = new();
}
'@

# ---------- 2. Core: парсер input.txt ----------
Write-Step "Core: парсер input.txt"

Write-File "LabApp/Core/InputParser.cs" @'
namespace LabApp.Core;

public static class InputParser
{
    public static Result<IReadOnlyList<Command>> Parse(string input)
    {
        if (input is null) return Fail("input is null");

        var tokens = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var commands = new List<Command>(tokens.Length);

        foreach (var t in tokens)
        {
            if (t.Contains(','))
            {
                var parts = t.Split(',', 2);
                if (parts[0] != "1")
                    return Fail($"команда с аргументом должна быть 1,<value>, получено: {t}");
                if (!ScalarValue.TryParse(parts[1], out var sv))
                    return Fail($"некорректное значение: {parts[1]}");
                commands.Add(new Command(CommandKind.PushOrEnqueue, sv));
                continue;
            }
            if (!int.TryParse(t, out var code) || code < 1 || code > 5)
                return Fail($"неизвестная команда: {t}");
            commands.Add(new Command((CommandKind)code));
        }
        return Result<IReadOnlyList<Command>>.Ok(commands);
    }

    private static Result<IReadOnlyList<Command>> Fail(string msg) =>
        Result<IReadOnlyList<Command>>.Fail(msg);
}
'@

# ---------- 3. Core: ScenarioRunner ----------
Write-Step "Core: ScenarioRunner"

Write-File "LabApp/Core/ScenarioRunner.cs" @'
using System.Diagnostics;

namespace LabApp.Core;

public interface ICommandTarget
{
    string Insert(ScalarValue v);
    string Remove();
    string Peek();
    string IsEmpty();
    string Print();
}

public sealed class StackTarget : ICommandTarget
{
    private readonly IStack<ScalarValue> _s;
    public StackTarget(IStack<ScalarValue> s) { _s = s; }
    public string Insert(ScalarValue v) { _s.Push(v); return $"push({v.Raw})"; }
    public string Remove() => $"pop -> {_s.Pop().Raw}";
    public string Peek() => $"top -> {_s.Top().Raw}";
    public string IsEmpty() => _s.IsEmpty() ? "true" : "false";
    public string Print() => "[" + string.Join(", ", _s.Print().Select(x => x.Raw)) + "]";
}

public sealed class QueueTarget : ICommandTarget
{
    private readonly IQueue<ScalarValue> _q;
    public QueueTarget(IQueue<ScalarValue> q) { _q = q; }
    public string Insert(ScalarValue v) { _q.Enqueue(v); return $"enqueue({v.Raw})"; }
    public string Remove() => $"dequeue -> {_q.Dequeue().Raw}";
    public string Peek() => $"peek -> {_q.Peek().Raw}";
    public string IsEmpty() => _q.IsEmpty() ? "true" : "false";
    public string Print() => "[" + string.Join(", ", _q.Print().Select(x => x.Raw)) + "]";
}

public static class ScenarioRunner
{
    public static ScenarioResult RunStack(string input, Func<IStack<ScalarValue>> factory)
        => Run(input, () => new StackTarget(factory()));

    public static ScenarioResult RunQueue(string input, Func<IQueue<ScalarValue>> factory)
        => Run(input, () => new QueueTarget(factory()));

    public static ScenarioResult Run(string input, Func<ICommandTarget> targetFactory)
    {
        var timing = new Timing();
        var sw = Stopwatch.StartNew();

        var parseResult = InputParser.Parse(input);
        timing.ParseMs = sw.Elapsed.TotalMilliseconds;

        if (!parseResult.IsSuccess)
        {
            return new ScenarioResult
            {
                Timing = timing,
                Trace = new[] { new TraceEvent(0, "Error", null, parseResult.Error ?? "parse error") },
                FinalState = "[]"
            };
        }

        var target = targetFactory();
        var commands = parseResult.Value;
        var trace = new List<TraceEvent>(commands.Count);

        sw.Restart();
        for (int i = 0; i < commands.Count; i++)
        {
            var cmd = commands[i];
            string result;
            try
            {
                result = cmd.Kind switch
                {
                    CommandKind.PushOrEnqueue => target.Insert(cmd.Argument!),
                    CommandKind.PopOrDequeue  => target.Remove(),
                    CommandKind.TopOrPeek     => target.Peek(),
                    CommandKind.IsEmpty       => target.IsEmpty(),
                    CommandKind.Print         => target.Print(),
                    _                         => "unknown"
                };
            }
            catch (Exception ex)
            {
                result = "Ошибка: " + ex.Message;
            }
            trace.Add(new TraceEvent(i, cmd.Kind.ToString(), cmd.Argument?.Raw, result));
        }
        timing.ExecuteMs = sw.Elapsed.TotalMilliseconds;

        var finalPrint = target.Print();
        return new ScenarioResult { Trace = trace, FinalState = finalPrint, Timing = timing };
    }
}
'@

# ---------- 4. Core: выражения ----------
Write-Step "Core: токенайзер и выражения"

Write-File "LabApp/Core/Token.cs" @'
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
'@

Write-File "LabApp/Core/Tokenizer.cs" @'
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
'@

Write-File "LabApp/Core/PostfixEvaluator.cs" @'
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
'@

Write-File "LabApp/Core/ShuntingYard.cs" @'
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
'@

# ---------- 5. Core: бенчмарки ----------
Write-Step "Core: BenchmarkRunner"

Write-File "LabApp/Core/BenchmarkRunner.cs" @'
using System.Diagnostics;

namespace LabApp.Core;

public sealed record BenchmarkPoint(int Size, double CustomMs, double StandardMs)
{
    public double Ratio => StandardMs > 0 ? CustomMs / StandardMs : 0;
}

public sealed record BenchmarkResult(
    string Structure,
    string Operation,
    IReadOnlyList<BenchmarkPoint> Points);

public static class BenchmarkRunner
{
    public static BenchmarkResult CompareStacks(
        int[] sizes,
        int warmupRuns = 3,
        int iterations = 7,
        int seed = 42)
    {
        var points = new List<BenchmarkPoint>(sizes.Length);
        foreach (var n in sizes)
        {
            var values = MakeValues(n, seed);
            double custom = Measure(() =>
            {
                var s = new LinkedListStack<ScalarValue>();
                foreach (var v in values) s.Push(v);
                while (!s.IsEmpty()) s.Pop();
            }, warmupRuns, iterations);

            double standard = Measure(() =>
            {
                var s = new StandardStackAdapter<ScalarValue>();
                foreach (var v in values) s.Push(v);
                while (!s.IsEmpty()) s.Pop();
            }, warmupRuns, iterations);

            points.Add(new BenchmarkPoint(n, custom, standard));
        }
        return new BenchmarkResult("Stack", "Push+Pop", points);
    }

    public static BenchmarkResult CompareQueues(
        int[] sizes,
        int warmupRuns = 3,
        int iterations = 7,
        int seed = 42)
    {
        var points = new List<BenchmarkPoint>(sizes.Length);
        foreach (var n in sizes)
        {
            var values = MakeValues(n, seed);
            double custom = Measure(() =>
            {
                var q = new ListQueue<ScalarValue>();
                foreach (var v in values) q.Enqueue(v);
                while (!q.IsEmpty()) q.Dequeue();
            }, warmupRuns, iterations);

            double standard = Measure(() =>
            {
                var q = new StandardQueueAdapter<ScalarValue>();
                foreach (var v in values) q.Enqueue(v);
                while (!q.IsEmpty()) q.Dequeue();
            }, warmupRuns, iterations);

            points.Add(new BenchmarkPoint(n, custom, standard));
        }
        return new BenchmarkResult("Queue", "Enqueue+Dequeue", points);
    }

    public static int[] DefaultSizes => new[] { 100, 1_000, 10_000, 100_000 };

    private static ScalarValue[] MakeValues(int n, int seed)
    {
        var rng = new Random(seed);
        var values = new ScalarValue[n];
        for (int i = 0; i < n; i++)
            values[i] = new ScalarValue(rng.Next(0, 1_000_000).ToString(), true);
        return values;
    }

    private static double Measure(Action run, int warmupRuns, int iterations)
    {
        for (int w = 0; w < warmupRuns; w++) run();

        var times = new double[iterations];
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            run();
            sw.Stop();
            times[i] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        return times[times.Length / 2];
    }
}
'@

# ---------- 6. Тесты ----------
Write-Step "Тесты"

Write-File "Tests/LabApp.Tests/InputParserTests.cs" @'
using LabApp.Core;
using Xunit;

public class InputParserTests
{
    [Fact] public void Parse_SampleInput_ReturnsCommands()
    {
        var result = InputParser.Parse("3 4 1,56 1,7 1,cat 2 5 4");
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(7, result.Value.Count);
        Assert.Equal(CommandKind.TopOrPeek, result.Value[0].Kind);
        Assert.Equal(CommandKind.IsEmpty, result.Value[1].Kind);
        Assert.Equal(CommandKind.PushOrEnqueue, result.Value[2].Kind);
        Assert.Equal("56", result.Value[2].Argument!.Raw);
        Assert.Equal("cat", result.Value[4].Argument!.Raw);
    }

    [Fact] public void Parse_Empty_ReturnsEmpty()
    {
        var result = InputParser.Parse("");
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact] public void Parse_InvalidCommand_Fails()
    {
        var result = InputParser.Parse("9");
        Assert.False(result.IsSuccess);
        Assert.Contains("неизвестная команда", result.Error);
    }

    [Fact] public void Parse_NonOneWithArgument_Fails()
    {
        var result = InputParser.Parse("2,foo");
        Assert.False(result.IsSuccess);
    }
}
'@

Write-File "Tests/LabApp.Tests/ScenarioRunnerTests.cs" @'
using LabApp.Core;
using Xunit;

public class ScenarioRunnerTests
{
    [Fact] public void RunStack_SimpleTrace()
    {
        var result = ScenarioRunner.RunStack("1,1 1,2 3 2 4", () => new LinkedListStack<ScalarValue>());
        Assert.Equal(5, result.Trace.Count);
        Assert.Contains("top -> 2", result.Trace[2].Result);
        Assert.Contains("pop -> 2", result.Trace[3].Result);
        Assert.Equal("false", result.Trace[4].Result);
        Assert.True(result.Timing.ExecuteMs >= 0);
    }

    [Fact] public void RunQueue_FIFO()
    {
        var result = ScenarioRunner.RunQueue("1,1 1,2 2", () => new ListQueue<ScalarValue>());
        Assert.Equal(3, result.Trace.Count);
        Assert.Contains("1", result.Trace[2].Result);
    }

    [Fact] public void RunStack_ErrorOnEmpty_IsRecordedNotThrown()
    {
        var result = ScenarioRunner.RunStack("2", () => new LinkedListStack<ScalarValue>());
        Assert.Single(result.Trace);
        Assert.Contains("Ошибка", result.Trace[0].Result);
    }

    [Fact] public void BothStacks_SameTrace()
    {
        var a = ScenarioRunner.RunStack("1,1 1,2 3 5 4", () => new LinkedListStack<ScalarValue>());
        var b = ScenarioRunner.RunStack("1,1 1,2 3 5 4", () => new StandardStackAdapter<ScalarValue>());
        Assert.Equal(a.Trace.Count, b.Trace.Count);
        for (int i = 0; i < a.Trace.Count; i++)
            Assert.Equal(a.Trace[i].Result, b.Trace[i].Result);
    }
}
'@

Write-File "Tests/LabApp.Tests/ExpressionTests.cs" @'
using LabApp.Core;
using Xunit;

public class ExpressionTests
{
    [Fact] public void Postfix_Sample_Returns3()
    {
        var tokens = Tokenizer.Tokenize("7 8 + 3 2 + /");
        Assert.True(tokens.IsSuccess, tokens.Error);
        var value = PostfixEvaluator.Evaluate(tokens.Value);
        Assert.True(value.IsSuccess, value.Error);
        Assert.Equal(3, value.Value, 3);
    }

    [Fact] public void Postfix_Add_Returns4()
    {
        var value = PostfixEvaluator.Evaluate(Tokenizer.Tokenize("2 2 +").Value);
        Assert.True(value.IsSuccess);
        Assert.Equal(4, value.Value, 3);
    }

    [Fact] public void Postfix_DivByZero_Fails()
    {
        var value = PostfixEvaluator.Evaluate(Tokenizer.Tokenize("1 0 /").Value);
        Assert.False(value.IsSuccess);
    }

    [Fact] public void Postfix_SqrtOfNegative_Fails()
    {
        var value = PostfixEvaluator.Evaluate(Tokenizer.Tokenize("-1 sqrt").Value);
        Assert.False(value.IsSuccess);
    }

    [Fact] public void ShuntingYard_Simple()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("2+2").Value);
        Assert.True(postfix.IsSuccess);
        Assert.Equal(new[] { "2", "2", "+" }, postfix.Value.Select(t => t.Lexeme).ToArray());
    }

    [Fact] public void ShuntingYard_Precedence()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("7+8*3").Value);
        Assert.Equal(new[] { "7", "8", "3", "*", "+" }, postfix.Value.Select(t => t.Lexeme).ToArray());
    }

    [Fact] public void ShuntingYard_Parens()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("(2+2)*3").Value);
        Assert.Equal(new[] { "2", "2", "+", "3", "*" }, postfix.Value.Select(t => t.Lexeme).ToArray());
    }

    [Fact] public void ShuntingYard_RightAssocPow()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("2^3^2").Value);
        Assert.Equal(new[] { "2", "3", "2", "^", "^" }, postfix.Value.Select(t => t.Lexeme).ToArray());
    }

    [Fact] public void ShuntingYard_UnbalancedParens_Fails()
    {
        var postfix = ShuntingYard.ToPostfix(Tokenizer.Tokenize("(2+2").Value);
        Assert.False(postfix.IsSuccess);
    }

    [Fact] public void Roundtrip_Infix_Postfix_Evaluates()
    {
        var infix = Tokenizer.Tokenize("(7+8)/(3+2)").Value;
        var postfix = ShuntingYard.ToPostfix(infix).Value;
        var value = PostfixEvaluator.Evaluate(postfix);
        Assert.True(value.IsSuccess, value.Error);
        Assert.Equal(3, value.Value, 3);
    }
}
'@

Write-File "Tests/LabApp.Tests/BenchmarkRunnerTests.cs" @'
using LabApp.Core;
using Xunit;

public class BenchmarkRunnerTests
{
    [Fact] public void CompareStacks_ProducesPoints()
    {
        var result = BenchmarkRunner.CompareStacks(new[] { 10, 100 }, warmupRuns: 1, iterations: 2);
        Assert.Equal("Stack", result.Structure);
        Assert.Equal(2, result.Points.Count);
        Assert.All(result.Points, p =>
        {
            Assert.True(p.CustomMs >= 0);
            Assert.True(p.StandardMs >= 0);
        });
    }

    [Fact] public void CompareQueues_ProducesPoints()
    {
        var result = BenchmarkRunner.CompareQueues(new[] { 10, 100 }, warmupRuns: 1, iterations: 2);
        Assert.Equal("Queue", result.Structure);
        Assert.Equal(2, result.Points.Count);
    }

    [Fact] public void CompareStacks_SizesArePreserved()
    {
        var sizes = new[] { 10, 50, 100 };
        var result = BenchmarkRunner.CompareStacks(sizes, warmupRuns: 1, iterations: 1);
        Assert.Equal(sizes, result.Points.Select(p => p.Size).ToArray());
    }
}
'@

# ---------- 7. Сборка ----------
Write-Step "Сборка"
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build завершился с ошибкой (код $LASTEXITCODE). Смотри вывод выше."
}

# ---------- 8. Тесты ----------
Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test завершился с ошибкой (код $LASTEXITCODE). Смотри вывод выше."
}

# ---------- 9. Итог ----------
Write-Step "Готово"
Write-Host "  Solution: $sln"
Write-Host "  Папка:    $rootPath"
Write-Host ""
Write-Host "  Что теперь работает:"
Write-Host "    - Парсинг input.txt (3 4 1,56 1,7 1,cat 2 5 4)"
Write-Host "    - Сценарии стека и очереди с пошаговым trace и таймингами"
Write-Host "    - Постфиксные выражения (7 8 + 3 2 + /)"
Write-Host "    - Инфикс -> постфикс (2+2 -> 2 2 +)"
Write-Host "    - Сравнение LinkedListStack vs StandardStackAdapter"
Write-Host "      и ListQueue vs StandardQueueAdapter (BenchmarkRunner)"
Write-Host ""
Write-Host "  Ожидаемый результат: ~36 passed, 0 failed"
Write-Host "  Следующий шаг: API-эндпоинты (Шаг 3)"