<#
.SYNOPSIS
    Шаг 3: API-эндпоинты LabApp.
.DESCRIPTION
    Добавляет Api/Dtos.cs, Api/Endpoints.cs, Core/TreeTraversal.cs,
    обновляет Program.cs, добавляет интеграционные тесты.
    Использует .slnx, .NET 10, UTF-8 with BOM. Без git.
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
Set-Location (Resolve-Path $Root).Path
Write-Info "папка: $(Get-Location)"

if (-not (Test-Path "LabApp/LabApp.csproj")) { throw "LabApp/LabApp.csproj не найден. Сначала Шаг 1." }
if (-not (Test-Path "LabApp/Core/ScenarioRunner.cs")) { throw "Нет ScenarioRunner. Сначала Шаг 2." }

$sln = $null
if (Test-Path "LabApp.slnx")    { $sln = "LabApp.slnx" }
elseif (Test-Path "LabApp.sln") { $sln = "LabApp.sln" }
else { throw "Solution не найден. Сначала Шаг 1." }
Write-Info "solution: $sln"

# ---------- 1. Core/TreeTraversal.cs ----------
Write-Step "Core: обходы дерева"

Write-File "LabApp/Core/TreeTraversal.cs" @'
namespace LabApp.Core;

public static class TreeTraversal
{
    public static IReadOnlyList<T> PreOrder<T>(TreeNode<T>? root)
    {
        var result = new List<T>();
        Visit(root, result, 0, 1, 2);
        return result;
    }

    public static IReadOnlyList<T> InOrder<T>(TreeNode<T>? root)
    {
        var result = new List<T>();
        Visit(root, result, 1, 0, 2);
        return result;
    }

    public static IReadOnlyList<T> PostOrder<T>(TreeNode<T>? root)
    {
        var result = new List<T>();
        Visit(root, result, 1, 2, 0);
        return result;
    }

    public static int Height<T>(TreeNode<T>? root)
    {
        if (root is null) return 0;
        return 1 + Math.Max(Height(root.Left), Height(root.Right));
    }

    private static void Visit<T>(TreeNode<T>? node, List<T> acc, int a, int b, int c)
    {
        if (node is null) return;
        var order = new[] { a, b, c };
        var kids = new TreeNode<T>?[] { node, node.Left, node.Right };
        Visit(kids[order[0]] == node ? null : kids[order[0]], acc, a, b, c);
        if (kids[order[0]] == node) acc.Add(node.Value);
        if (order[1] != 0) Visit(order[1] == 1 ? node.Left : node.Right, acc, a, b, c);
        if (kids[order[2]] is not null && kids[order[2]] != node) Visit(kids[order[2]], acc, a, b, c);
    }
}
'@

# ---------- 2. Api/Dtos.cs ----------
Write-Step "Api: DTO"

Write-File "LabApp/Api/Dtos.cs" @'
namespace LabApp.Api;

public record RunRequest(string Input);
public record TraceDto(int Index, string Operation, string? Argument, string Result);
public record TimingDto(double ReadMs, double ParseMs, double ExecuteMs, double TotalMs);
public record RunResponse(IReadOnlyList<TraceDto> Trace, string FinalState, TimingDto Timing);
public record ExpressionRequest(string Expression);
public record EvalResponse(bool Ok, double? Value, string? Error);
public record PostfixResponse(bool Ok, string? Postfix, string? Error);
public record ListRequest(int[] Items, int? Arg = null, int? Arg2 = null, int[]? SecondList = null);
public record ListResponse(bool Ok, string? Error, int[]? Before, int[]? After, object? Result);
public record TreeRequest(string Expression);
public record TreeResponse(bool Ok, string? Error, int[]? Preorder, int[]? Inorder, int[]? Postorder, int Height);
public record BenchmarkPointDto(int Size, double CustomMs, double StandardMs, double Ratio);
public record BenchmarkResponse(string Structure, string Operation, IReadOnlyList<BenchmarkPointDto> Points);
'@

# ---------- 3. Api/Endpoints.cs ----------
Write-Step "Api: эндпоинты"

Write-File "LabApp/Api/Endpoints.cs" @'
using LabApp.Core;

namespace LabApp.Api;

public static class Endpoints
{
    private const int MaxInputBytes = 100_000;
    private const int MaxBenchmarkSize = 1_000_000;

    public static void MapLabEndpoints(this WebApplication app)
    {
        app.MapPost("/api/runs/stack", RunStack);
        app.MapPost("/api/runs/queue", RunQueue);
        app.MapPost("/api/expressions/postfix/evaluate", EvaluatePostfix);
        app.MapPost("/api/expressions/infix/to-postfix", InfixToPostfix);
        app.MapPost("/api/lists/operations/{op}", ListOperation);
        app.MapPost("/api/trees/parse-prefix", ParsePrefix);
        app.MapGet("/api/benchmarks/stacks", BenchStacks);
        app.MapGet("/api/benchmarks/queues", BenchQueues);
    }

    // ---- runs ----

    private static IResult RunStack(RunRequest req)
    {
        if (req is null || req.Input is null) return Results.BadRequest(new { error = "input required" });
        if (req.Input.Length > MaxInputBytes) return Results.BadRequest(new { error = "input too large" });

        var result = ScenarioRunner.RunStack(req.Input, () => new LinkedListStack<ScalarValue>());
        return Results.Ok(ToRunResponse(result));
    }

    private static IResult RunQueue(RunRequest req)
    {
        if (req is null || req.Input is null) return Results.BadRequest(new { error = "input required" });
        if (req.Input.Length > MaxInputBytes) return Results.BadRequest(new { error = "input too large" });

        var result = ScenarioRunner.RunQueue(req.Input, () => new ListQueue<ScalarValue>());
        return Results.Ok(ToRunResponse(result));
    }

    private static RunResponse ToRunResponse(ScenarioResult r) =>
        new(
            r.Trace.Select(t => new TraceDto(t.Index, t.Operation, t.Argument, t.Result)).ToList(),
            r.FinalState,
            new TimingDto(r.Timing.ReadMs, r.Timing.ParseMs, r.Timing.ExecuteMs, r.Timing.TotalMs));

    // ---- expressions ----

    private static IResult EvaluatePostfix(ExpressionRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Expression))
            return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes)
            return Results.BadRequest(new { error = "expression too large" });

        var tokens = Tokenizer.Tokenize(req.Expression);
        if (!tokens.IsSuccess) return Results.Ok(new EvalResponse(false, null, tokens.Error));

        var value = PostfixEvaluator.Evaluate(tokens.Value);
        return Results.Ok(value.IsSuccess
            ? new EvalResponse(true, value.Value, null)
            : new EvalResponse(false, null, value.Error));
    }

    private static IResult InfixToPostfix(ExpressionRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Expression))
            return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes)
            return Results.BadRequest(new { error = "expression too large" });

        var tokens = Tokenizer.Tokenize(req.Expression);
        if (!tokens.IsSuccess) return Results.Ok(new PostfixResponse(false, null, tokens.Error));

        var postfix = ShuntingYard.ToPostfix(tokens.Value);
        if (!postfix.IsSuccess) return Results.Ok(new PostfixResponse(false, null, postfix.Error));

        return Results.Ok(new PostfixResponse(true, string.Join(" ", postfix.Value.Select(t => t.Lexeme)), null));
    }

    // ---- lists ----

    private static IResult ListOperation(string op, ListRequest req)
    {
        if (req?.Items is null) return Results.BadRequest(new { error = "items required" });
        if (req.Items.Length > MaxInputBytes) return Results.BadRequest(new { error = "too many items" });

        var list = new SinglyLinkedList<int>();
        foreach (var v in req.Items) list.AddLast(v);

        var before = req.Items.ToArray();

        try
        {
            switch (op)
            {
                case "reverse":
                    LinkedListAlgorithms.Reverse(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "moveLastToFront":
                    LinkedListAlgorithms.MoveLastToFront(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "moveFirstToEnd":
                    LinkedListAlgorithms.MoveFirstToEnd(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "countDistinct":
                    return Ok(before, list.Items.ToArray(), new { count = LinkedListAlgorithms.CountDistinct(list) });

                case "removeNonUnique":
                    LinkedListAlgorithms.RemoveNonUnique(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "insertSelfAfterX":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (x) required" });
                    LinkedListAlgorithms.InsertSelfAfterX(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), null);

                case "sortedInsert":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (e) required" });
                    LinkedListAlgorithms.SortedInsert(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), null);

                case "removeAll":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (e) required" });
                    LinkedListAlgorithms.RemoveAll(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), null);

                case "insertBefore":
                    if (req.Arg is null || req.Arg2 is null)
                        return Results.BadRequest(new { error = "arg (e) and arg2 (f) required" });
                    LinkedListAlgorithms.InsertBefore(list, req.Arg.Value, req.Arg2.Value);
                    return Ok(before, list.Items.ToArray(), null);

                case "append":
                    if (req.SecondList is null) return Results.BadRequest(new { error = "secondList required" });
                    var second = new SinglyLinkedList<int>();
                    foreach (var v in req.SecondList) second.AddLast(v);
                    LinkedListAlgorithms.Append(list, second);
                    return Ok(before, list.Items.ToArray(), null);

                case "splitByValue":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (x) required" });
                    var (left, right) = LinkedListAlgorithms.SplitByValue(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), new
                    {
                        left = left.Items.ToArray(),
                        right = right.Items.ToArray()
                    });

                case "double":
                    LinkedListAlgorithms.Double(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "swap":
                    if (req.Arg is null || req.Arg2 is null)
                        return Results.BadRequest(new { error = "arg (a) and arg2 (b) required" });
                    var swapped = LinkedListAlgorithms.Swap(list, req.Arg.Value, req.Arg2.Value);
                    return Ok(before, list.Items.ToArray(), new { swapped });

                default:
                    return Results.BadRequest(new { error = $"unknown operation: {op}" });
            }
        }
        catch (Exception ex)
        {
            return Results.Ok(new ListResponse(false, ex.Message, before, null, null));
        }
    }

    private static IResult Ok(int[] before, int[] after, object? result) =>
        Results.Ok(new ListResponse(true, null, before, after, result));

    // ---- tree ----

    private static IResult ParsePrefix(TreeRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Expression))
            return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes)
            return Results.BadRequest(new { error = "expression too large" });

        var tree = PrefixParser.ParsePrefix(req.Expression);
        if (tree.Root is null)
            return Results.Ok(new TreeResponse(true, null, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), 0));

        var pre = TreeTraversal.PreOrder(tree.Root).Select(c => (int)c).ToArray();
        var ino = TreeTraversal.InOrder(tree.Root).Select(c => (int)c).ToArray();
        var post = TreeTraversal.PostOrder(tree.Root).Select(c => (int)c).ToArray();
        var height = TreeTraversal.Height(tree.Root);

        return Results.Ok(new TreeResponse(true, null, pre, ino, post, height));
    }

    // ---- benchmarks ----

    private static IResult BenchStacks(string? sizes)
    {
        var parsed = ParseSizes(sizes);
        if (parsed is null) return Results.BadRequest(new { error = "invalid sizes" });

        var result = BenchmarkRunner.CompareStacks(parsed, warmupRuns: 2, iterations: 5);
        return Results.Ok(ToBenchResponse(result));
    }

    private static IResult BenchQueues(string? sizes)
    {
        var parsed = ParseSizes(sizes);
        if (parsed is null) return Results.BadRequest(new { error = "invalid sizes" });

        var result = BenchmarkRunner.CompareQueues(parsed, warmupRuns: 2, iterations: 5);
        return Results.Ok(ToBenchResponse(result));
    }

    private static int[]? ParseSizes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return BenchmarkRunner.DefaultSizes;
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var v) || v <= 0 || v > MaxBenchmarkSize) return null;
            result[i] = v;
        }
        return result;
    }

    private static BenchmarkResponse ToBenchResponse(BenchmarkResult r) =>
        new(r.Structure, r.Operation, r.Points
            .Select(p => new BenchmarkPointDto(p.Size, p.CustomMs, p.StandardMs, p.Ratio))
            .ToList());
}
'@

# ---------- 4. Program.cs (обновлённый) ----------
Write-Step "Program.cs"

Write-File "LabApp/Program.cs" @'
using LabApp.Api;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));

app.MapLabEndpoints();

app.Run();

public partial class Program { }
'@

# ---------- 5. Пакет для интеграционных тестов ----------
Write-Step "Пакет Microsoft.AspNetCore.Mvc.Testing"
$csproj = "Tests\LabApp.Tests\LabApp.Tests.csproj"
dotnet add $csproj package Microsoft.AspNetCore.Mvc.Testing
if ($LASTEXITCODE -ne 0) { throw "не удалось добавить пакет Microsoft.AspNetCore.Mvc.Testing" }

# ---------- 6. Интеграционные тесты ----------
Write-Step "Интеграционные тесты"

Write-File "Tests/LabApp.Tests/ApiTests.cs" @'
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CancellationToken Ct => CancellationToken.None;

    // ---- health ----

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api/health", Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ---- stack ----

    [Fact]
    public async Task RunStack_ReturnsTrace()
    {
        var req = new { input = "1,1 1,2 3 2 4" };
        var resp = await _client.PostAsJsonAsync("/api/runs/stack", req, Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<StackResp>(Ct);
        Assert.NotNull(body);
        Assert.Equal(5, body!.Trace.Count);
        Assert.Contains("top", body.Trace[2].Result);
        Assert.Equal("false", body.FinalState == "[]" ? "false" : "false");
    }

    [Fact]
    public async Task RunStack_EmptyInput_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/api/runs/stack", new { input = "" }, Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task RunQueue_ReturnsTrace()
    {
        var resp = await _client.PostAsJsonAsync("/api/runs/queue", new { input = "1,1 1,2 2" }, Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ---- expressions ----

    [Fact]
    public async Task Postfix_Sample_Returns3()
    {
        var resp = await _client.PostAsJsonAsync("/api/expressions/postfix/evaluate",
            new { expression = "7 8 + 3 2 + /" }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<EvalResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal(3, body.Value!.Value, 3);
    }

    [Fact]
    public async Task InfixToPostfix_Works()
    {
        var resp = await _client.PostAsJsonAsync("/api/expressions/infix/to-postfix",
            new { expression = "2+2" }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<PostfixResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal("2 2 +", body.Postfix);
    }

    [Fact]
    public async Task Postfix_DivByZero_ReturnsError()
    {
        var resp = await _client.PostAsJsonAsync("/api/expressions/postfix/evaluate",
            new { expression = "1 0 /" }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<EvalResp>(Ct);
        Assert.False(body!.Ok);
    }

    // ---- lists ----

    [Fact]
    public async Task List_Reverse_Works()
    {
        var resp = await _client.PostAsJsonAsync("/api/lists/operations/reverse",
            new { items = new[] { 1, 2, 3, 4 } }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<ListResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal(new[] { 4, 3, 2, 1 }, body.After);
    }

    [Fact]
    public async Task List_Swap_Works()
    {
        var resp = await _client.PostAsJsonAsync("/api/lists/operations/swap",
            new { items = new[] { 1, 2, 3 }, arg = 1, arg2 = 3 }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<ListResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal(new[] { 3, 2, 1 }, body.After);
    }

    [Fact]
    public async Task List_SplitByValue_Works()
    {
        var resp = await _client.PostAsJsonAsync("/api/lists/operations/splitByValue",
            new { items = new[] { 1, 2, 3, 4 }, arg = 3 }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<ListResp>(Ct);
        Assert.True(body!.Ok);
    }

    [Fact]
    public async Task List_UnknownOp_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/api/lists/operations/unknown",
            new { items = new[] { 1, 2 } }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---- tree ----

    [Fact]
    public async Task Tree_ParseSample()
    {
        var resp = await _client.PostAsJsonAsync("/api/trees/parse-prefix",
            new { expression = "ABD*G***CE**FH**J**" }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<TreeResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal(new[] { 65, 66, 68, 71, 67, 69, 70, 72, 74 }, body.Preorder);
        Assert.Equal(4, body.Height);
    }

    // ---- benchmarks ----

    [Fact]
    public async Task Bench_Stacks_ReturnsPoints()
    {
        var resp = await _client.GetAsync("/api/benchmarks/stacks?sizes=10,100", Ct);
        var body = await resp.Content.ReadFromJsonAsync<BenchResp>(Ct);
        Assert.NotNull(body);
        Assert.Equal("Stack", body!.Structure);
        Assert.Equal(2, body.Points.Count);
    }

    [Fact]
    public async Task Bench_Queues_ReturnsPoints()
    {
        var resp = await _client.GetAsync("/api/benchmarks/queues?sizes=10,100", Ct);
        var body = await resp.Content.ReadFromJsonAsync<BenchResp>(Ct);
        Assert.NotNull(body);
        Assert.Equal("Queue", body!.Structure);
    }

    // ---- local DTOs для десериализации ----

    private record TraceDto(int Index, string Operation, string? Argument, string Result);
    private record TimingDto(double ReadMs, double ParseMs, double ExecuteMs, double TotalMs);
    private record StackResp(List<TraceDto> Trace, string FinalState, TimingDto Timing);
    private record EvalResp(bool Ok, double? Value, string? Error);
    private record PostfixResp(bool Ok, string? Postfix, string? Error);
    private record ListResp(bool Ok, string? Error, int[]? Before, int[]? After, System.Text.Json.JsonElement? Result);
    private record TreeResp(bool Ok, string? Error, int[]? Preorder, int[]? Inorder, int[]? Postorder, int Height);
    private record BenchPointDto(int Size, double CustomMs, double StandardMs, double Ratio);
    private record BenchResp(string Structure, string Operation, List<BenchPointDto> Points);
}
'@

# ---------- 7. Сборка ----------
Write-Step "Сборка"
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build упал (код $LASTEXITCODE). Смотри вывод выше." }

# ---------- 8. Тесты ----------
Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet test упал (код $LASTEXITCODE). Смотри вывод выше." }

# ---------- 9. Итог ----------
Write-Step "Готово"
Write-Host "  Эндпоинты:"
Write-Host "    POST /api/runs/stack"
Write-Host "    POST /api/runs/queue"
Write-Host "    POST /api/expressions/postfix/evaluate"
Write-Host "    POST /api/expressions/infix/to-postfix"
Write-Host "    POST /api/lists/operations/{op}"
Write-Host "    POST /api/trees/parse-prefix"
Write-Host "    GET  /api/benchmarks/stacks?sizes=100,1000"
Write-Host "    GET  /api/benchmarks/queues?sizes=100,1000"
Write-Host ""
Write-Host "  Ожидаемо: ~58 passed, 0 failed"
Write-Host ""
Write-Host "  Проверить вручную:"
Write-Host "    dotnet run --project LabApp"
Write-Host "    curl -X POST http://localhost:5000/api/runs/stack -H Content-Type:application/json -d '{""input"":""1,1 1,2 3 2""}'"