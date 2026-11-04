<#
.SYNOPSIS
    Шаг 4: фронтенд LabApp + расширение TreeResponse.
.DESCRIPTION
    Обновляет Dtos.cs и Endpoints.cs (Root в TreeResponse),
    пишет wwwroot/index.html, styles.css, app.js и docs/REPORT.md.
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
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet не найден" }
Set-Location (Resolve-Path $Root).Path
Write-Info "папка: $(Get-Location)"

if (-not (Test-Path "LabApp/Api/Endpoints.cs")) { throw "Нет Endpoints.cs. Сначала Шаг 3." }

$sln = $null
if (Test-Path "LabApp.slnx")    { $sln = "LabApp.slnx" }
elseif (Test-Path "LabApp.sln") { $sln = "LabApp.sln" }
else { throw "Solution не найден." }
Write-Info "solution: $sln"

# ---------- 1. Dtos.cs — Root в TreeResponse ----------
Write-Step "Обновление Dtos.cs"

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
public record TreeNodeDto(string Value, TreeNodeDto? Left, TreeNodeDto? Right);
public record TreeResponse(
    bool Ok,
    string? Error,
    TreeNodeDto? Root,
    int[]? Preorder,
    int[]? Inorder,
    int[]? Postorder,
    int Height);
public record BenchmarkPointDto(int Size, double CustomMs, double StandardMs, double Ratio);
public record BenchmarkResponse(string Structure, string Operation, IReadOnlyList<BenchmarkPointDto> Points);
'@

# ---------- 2. Endpoints.cs — Root в ParsePrefix ----------
Write-Step "Обновление Endpoints.cs"

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

    private static IResult ParsePrefix(TreeRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Expression))
            return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes)
            return Results.BadRequest(new { error = "expression too large" });

        var tree = PrefixParser.ParsePrefix(req.Expression);
        if (tree.Root is null)
        {
            return Results.Ok(new TreeResponse(
                true, null, null,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), 0));
        }

        var pre = TreeTraversal.PreOrder(tree.Root).Select(c => (int)c).ToArray();
        var ino = TreeTraversal.InOrder(tree.Root).Select(c => (int)c).ToArray();
        var post = TreeTraversal.PostOrder(tree.Root).Select(c => (int)c).ToArray();
        var height = TreeTraversal.Height(tree.Root);
        var root = ToNodeDto(tree.Root);

        return Results.Ok(new TreeResponse(true, null, root, pre, ino, post, height));
    }

    private static TreeNodeDto? ToNodeDto<T>(TreeNode<T>? node)
    {
        if (node is null) return null;
        return new TreeNodeDto(
            node.Value?.ToString() ?? "",
            ToNodeDto(node.Left),
            ToNodeDto(node.Right));
    }

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

# ---------- 3. wwwroot/index.html ----------
Write-Step "wwwroot: index.html"

Write-File "LabApp/wwwroot/index.html" @'
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>LabApp — практическая 3</title>
<link rel="stylesheet" href="styles.css">
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
</head>
<body>
<header>
  <h1>LabApp</h1>
  <p class="muted">Структуры данных: стек, очередь, выражения, списки, дерево. Сравнение со стандартными коллекциями .NET.</p>
  <nav class="tabs">
    <button class="tab active" data-tab="stack">Stack</button>
    <button class="tab" data-tab="queue">Queue</button>
    <button class="tab" data-tab="expr">Expressions</button>
    <button class="tab" data-tab="lists">Lists</button>
    <button class="tab" data-tab="tree">Tree</button>
    <button class="tab" data-tab="bench">Benchmarks</button>
  </nav>
</header>

<main>
  <!-- Stack -->
  <section id="panel-stack" class="panel active">
    <h2>Стек</h2>
    <p class="muted">Формат: <code>1,val</code> — push; <code>2</code> — pop; <code>3</code> — top; <code>4</code> — isEmpty; <code>5</code> — print.</p>
    <textarea id="stack-input" rows="3">3 4 1,56 1,7 1,cat 2 5 4</textarea>
    <button id="stack-run">Выполнить</button>
    <div id="stack-output" class="output"></div>
  </section>

  <!-- Queue -->
  <section id="panel-queue" class="panel">
    <h2>Очередь</h2>
    <p class="muted">Те же команды: 1 — enqueue, 2 — dequeue, 3 — peek, 4 — isEmpty, 5 — print.</p>
    <textarea id="queue-input" rows="3">1,56 1,7 1,cat 3 2 5 4</textarea>
    <button id="queue-run">Выполнить</button>
    <div id="queue-output" class="output"></div>
  </section>

  <!-- Expressions -->
  <section id="panel-expr" class="panel">
    <h2>Выражения</h2>
    <h3>Постфиксное</h3>
    <input id="postfix-input" type="text" value="7 8 + 3 2 + /">
    <button id="postfix-eval">Вычислить</button>
    <div id="postfix-output" class="output"></div>

    <h3>Инфикс → постфикс</h3>
    <input id="infix-input" type="text" value="(7+8)/(3+2)">
    <button id="infix-convert">Перевести</button>
    <div id="infix-output" class="output"></div>
  </section>

  <!-- Lists -->
  <section id="panel-lists" class="panel">
    <h2>Связный список</h2>
    <label>Элементы (через запятую): <input id="list-items" type="text" value="3, 1, 4, 1, 5, 9, 2, 6"></label>
    <label>Операция:
      <select id="list-op">
        <option value="reverse">reverse</option>
        <option value="moveLastToFront">moveLastToFront</option>
        <option value="moveFirstToEnd">moveFirstToEnd</option>
        <option value="countDistinct">countDistinct</option>
        <option value="removeNonUnique">removeNonUnique</option>
        <option value="insertSelfAfterX">insertSelfAfterX</option>
        <option value="sortedInsert">sortedInsert</option>
        <option value="removeAll">removeAll</option>
        <option value="insertBefore">insertBefore</option>
        <option value="append">append</option>
        <option value="splitByValue">splitByValue</option>
        <option value="double">double</option>
        <option value="swap">swap</option>
      </select>
    </label>
    <div id="list-args"></div>
    <button id="list-run">Применить</button>
    <div id="list-output" class="output"></div>
  </section>

  <!-- Tree -->
  <section id="panel-tree" class="panel">
    <h2>Дерево из префиксной записи</h2>
    <p class="muted">Маркер null — <code>*</code>. Пример: <code>ABD*G***CE**FH**J**</code></p>
    <input id="tree-input" type="text" value="ABD*G***CE**FH**J**">
    <button id="tree-run">Построить</button>
    <div id="tree-output" class="output"></div>
  </section>

  <!-- Benchmarks -->
  <section id="panel-bench" class="panel">
    <h2>Сравнение со стандартными структурами</h2>
    <label>Размеры входа (через запятую): <input id="bench-sizes" type="text" value="100,1000,10000,100000"></label>
    <div class="row">
      <button id="bench-stack">Стек: своя vs стандартная</button>
      <button id="bench-queue">Очередь: своя vs стандартная</button>
    </div>
    <div class="chart-wrap">
      <canvas id="bench-chart"></canvas>
    </div>
    <div id="bench-table" class="output"></div>
    <p class="muted small">Оси логарифмические. Красная — своя структура, синяя — стандартная. Ratio = своя / стандартная.</p>
  </section>
</main>

<script src="app.js"></script>
</body>
</html>
'@

# ---------- 4. wwwroot/styles.css ----------
Write-Step "wwwroot: styles.css"

Write-File "LabApp/wwwroot/styles.css" @'
:root {
  --fg: #1f2937;
  --muted: #6b7280;
  --accent: #0b6bcb;
  --bg: #f9fafb;
  --card: #ffffff;
  --line: #e5e7eb;
}
* { box-sizing: border-box; }
body {
  font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  color: var(--fg);
  background: var(--bg);
  margin: 0;
  padding: 0;
}
header {
  background: var(--card);
  border-bottom: 1px solid var(--line);
  padding: 1.5rem 2rem 0;
}
header h1 { margin: 0 0 .25rem; }
.muted { color: var(--muted); }
.small { font-size: 0.85rem; }

.tabs {
  display: flex;
  gap: .25rem;
  margin-top: 1rem;
}
.tab {
  background: transparent;
  border: 1px solid transparent;
  border-bottom: none;
  padding: .6rem 1.1rem;
  cursor: pointer;
  font-size: 1rem;
  color: var(--muted);
  border-radius: 6px 6px 0 0;
}
.tab:hover { color: var(--fg); }
.tab.active {
  color: var(--fg);
  background: var(--bg);
  border-color: var(--line);
  font-weight: 600;
  position: relative;
  bottom: -1px;
}

main { padding: 1.5rem 2rem 3rem; max-width: 1100px; }
.panel { display: none; }
.panel.active { display: block; }

h2 { margin-top: 0; }
input[type="text"], textarea, select {
  font-family: inherit;
  font-size: 1rem;
  padding: .5rem .75rem;
  border: 1px solid var(--line);
  border-radius: 6px;
  background: var(--card);
}
textarea { width: 100%; resize: vertical; }
input[type="text"] { min-width: 280px; }
label { display: block; margin: .75rem 0; }
button {
  background: var(--accent);
  color: white;
  border: none;
  padding: .55rem 1.1rem;
  font-size: 1rem;
  border-radius: 6px;
  cursor: pointer;
  margin-top: .5rem;
}
button:hover { filter: brightness(0.95); }
button + button { margin-left: .5rem; }

.output {
  margin-top: 1rem;
  padding: 1rem;
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 8px;
  min-height: 2.5rem;
}

.trace { width: 100%; border-collapse: collapse; font-size: 0.92rem; }
.trace th, .trace td { border-bottom: 1px solid var(--line); padding: .35rem .5rem; text-align: left; }
.trace th { color: var(--muted); font-weight: 500; }

.timing { color: var(--muted); font-size: .9rem; margin-bottom: .5rem; }
.final { margin: .5rem 0; }

code {
  background: #eef2ff;
  color: #3730a3;
  padding: .1rem .35rem;
  border-radius: 4px;
  font-size: .9rem;
}

.chart-wrap {
  margin-top: 1rem;
  background: var(--card);
  padding: 1rem;
  border: 1px solid var(--line);
  border-radius: 8px;
  height: 420px;
}
.row { display: flex; gap: .5rem; flex-wrap: wrap; }
.tree-svg svg { max-width: 100%; }
'@

# ---------- 5. wwwroot/app.js ----------
Write-Step "wwwroot: app.js"

Write-File "LabApp/wwwroot/app.js" @'
// ---------- утилиты ----------
const API = "/api";

document.querySelectorAll(".tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach(t => t.classList.remove("active"));
    document.querySelectorAll(".panel").forEach(p => p.classList.remove("active"));
    tab.classList.add("active");
    document.getElementById("panel-" + tab.dataset.tab).classList.add("active");
  });
});

async function post(path, body) {
  const r = await fetch(API + path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  return r.json();
}
async function get(path) {
  const r = await fetch(API + path);
  return r.json();
}

function esc(s) {
  return String(s).replace(/[&<>"']/g, c => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  }[c]));
}

function renderTrace(trace) {
  if (!trace || trace.length === 0) return "<em>нет событий</em>";
  const rows = trace.map(t =>
    "<tr><td>" + t.index + "</td><td>" + esc(t.operation) + "</td><td>" +
    esc(t.argument ?? "") + "</td><td>" + esc(t.result) + "</td></tr>"
  ).join("");
  return "<table class=\"trace\"><thead><tr><th>#</th><th>Операция</th><th>Аргумент</th><th>Результат</th></tr></thead><tbody>" + rows + "</tbody></table>";
}

function renderTiming(t) {
  return "<div class=\"timing\">read: " + t.readMs.toFixed(3) + " ms · parse: " +
    t.parseMs.toFixed(3) + " ms · execute: " + t.executeMs.toFixed(3) +
    " ms · total: " + t.totalMs.toFixed(3) + " ms</div>";
}

// ---------- stack ----------
document.getElementById("stack-run").addEventListener("click", async () => {
  const input = document.getElementById("stack-input").value;
  const out = document.getElementById("stack-output");
  out.textContent = "Выполняется...";
  try {
    const r = await post("/runs/stack", { input });
    out.innerHTML = renderTiming(r.timing) +
      "<div class=\"final\">Итог: <code>" + esc(r.finalState) + "</code></div>" +
      renderTrace(r.trace);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

// ---------- queue ----------
document.getElementById("queue-run").addEventListener("click", async () => {
  const input = document.getElementById("queue-input").value;
  const out = document.getElementById("queue-output");
  out.textContent = "Выполняется...";
  try {
    const r = await post("/runs/queue", { input });
    out.innerHTML = renderTiming(r.timing) +
      "<div class=\"final\">Итог: <code>" + esc(r.finalState) + "</code></div>" +
      renderTrace(r.trace);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

// ---------- expressions ----------
document.getElementById("postfix-eval").addEventListener("click", async () => {
  const expression = document.getElementById("postfix-input").value;
  const out = document.getElementById("postfix-output");
  out.textContent = "...";
  try {
    const r = await post("/expressions/postfix/evaluate", { expression });
    out.innerHTML = r.ok
      ? "Результат: <code>" + r.value + "</code>"
      : "Ошибка: " + esc(r.error);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

document.getElementById("infix-convert").addEventListener("click", async () => {
  const expression = document.getElementById("infix-input").value;
  const out = document.getElementById("infix-output");
  out.textContent = "...";
  try {
    const r = await post("/expressions/infix/to-postfix", { expression });
    out.innerHTML = r.ok
      ? "Постфиксная: <code>" + esc(r.postfix) + "</code>"
      : "Ошибка: " + esc(r.error);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

// ---------- lists ----------
const LIST_SPECS = {
  reverse: {},
  moveLastToFront: {},
  moveFirstToEnd: {},
  countDistinct: {},
  removeNonUnique: {},
  insertSelfAfterX: { arg: "x" },
  sortedInsert: { arg: "e" },
  removeAll: { arg: "e" },
  insertBefore: { arg: "e", arg2: "f" },
  append: { secondList: true },
  splitByValue: { arg: "x" },
  double: {},
  swap: { arg: "a", arg2: "b" }
};

function updateListForm() {
  const op = document.getElementById("list-op").value;
  const spec = LIST_SPECS[op] || {};
  const form = document.getElementById("list-args");
  let html = "";
  if (spec.arg) html += "<label>" + spec.arg + ": <input id=\"list-arg\" type=\"number\" value=\"3\"></label>";
  if (spec.arg2) html += "<label>" + spec.arg2 + ": <input id=\"list-arg2\" type=\"number\" value=\"5\"></label>";
  if (spec.secondList) html += "<label>secondList (через запятую): <input id=\"list-second\" type=\"text\" value=\"10, 20, 30\"></label>";
  form.innerHTML = html;
}

document.getElementById("list-op").addEventListener("change", updateListForm);
updateListForm();

document.getElementById("list-run").addEventListener("click", async () => {
  const op = document.getElementById("list-op").value;
  const items = document.getElementById("list-items").value
    .split(",").map(x => parseInt(x.trim(), 10)).filter(x => !isNaN(x));
  const body = { items };

  const arg = document.getElementById("list-arg"); if (arg) body.arg = parseInt(arg.value, 10);
  const arg2 = document.getElementById("list-arg2"); if (arg2) body.arg2 = parseInt(arg2.value, 10);
  const second = document.getElementById("list-second"); if (second) body.secondList = second.value
    .split(",").map(x => parseInt(x.trim(), 10)).filter(x => !isNaN(x));

  const out = document.getElementById("list-output");
  out.textContent = "...";
  try {
    const r = await post("/lists/operations/" + op, body);
    if (!r.ok) { out.innerHTML = "Ошибка: " + esc(r.error); return; }
    let html = "<div>До: <code>[" + r.before.join(", ") + "]</code></div>" +
               "<div>После: <code>[" + r.after.join(", ") + "]</code></div>";
    if (r.result) html += "<div>Результат: <code>" + esc(JSON.stringify(r.result)) + "</code></div>";
    out.innerHTML = html;
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

// ---------- tree ----------
document.getElementById("tree-run").addEventListener("click", async () => {
  const expression = document.getElementById("tree-input").value;
  const out = document.getElementById("tree-output");
  out.textContent = "...";
  try {
    const r = await post("/trees/parse-prefix", { expression });
    if (!r.ok) { out.innerHTML = "Ошибка: " + esc(r.error); return; }
    const pre = String.fromCharCode.apply(null, r.preorder);
    const ino = String.fromCharCode.apply(null, r.inorder);
    const post = String.fromCharCode.apply(null, r.postorder);
    out.innerHTML =
      "<div>Preorder: <code>" + esc(pre) + "</code></div>" +
      "<div>Inorder: <code>" + esc(ino) + "</code></div>" +
      "<div>Postorder: <code>" + esc(post) + "</code></div>" +
      "<div>Высота: " + r.height + "</div>" +
      "<div class=\"tree-svg\">" + renderTree(r.root) + "</div>";
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

function renderTree(root) {
  if (!root) return "<em>пусто</em>";
  const nodes = [];
  let nextX = 0;
  const XGAP = 46, LEVEL = 64, R = 18;

  function place(n, depth) {
    if (!n) return;
    place(n.left, depth + 1);
    n._x = nextX * XGAP;
    n._y = depth * LEVEL;
    nextX++;
    nodes.push(n);
    place(n.right, depth + 1);
  }
  place(root, 0);

  let maxX = 0, maxY = 0;
  for (let i = 0; i < nodes.length; i++) {
    if (nodes[i]._x > maxX) maxX = nodes[i]._x;
    if (nodes[i]._y > maxY) maxY = nodes[i]._y;
  }

  const edges = [];
  function drawEdges(n) {
    if (!n) return;
    if (n.left) edges.push("<line x1=\"" + (n._x + R) + "\" y1=\"" + (n._y + R) +
      "\" x2=\"" + (n.left._x + R) + "\" y2=\"" + (n.left._y + R) + "\" stroke=\"#9ca3af\" />");
    if (n.right) edges.push("<line x1=\"" + (n._x + R) + "\" y1=\"" + (n._y + R) +
      "\" x2=\"" + (n.right._x + R) + "\" y2=\"" + (n.right._y + R) + "\" stroke=\"#9ca3af\" />");
    drawEdges(n.left);
    drawEdges(n.right);
  }
  drawEdges(root);

  const circles = nodes.map(n =>
    "<circle cx=\"" + (n._x + R) + "\" cy=\"" + (n._y + R) + "\" r=\"" + R + "\" fill=\"#0b6bcb\" />" +
    "<text x=\"" + (n._x + R) + "\" y=\"" + (n._y + R + 5) + "\" text-anchor=\"middle\" fill=\"white\" font-size=\"14\">" + esc(n.value) + "</text>"
  ).join("");

  return "<svg width=\"" + (maxX + 2 * R + 10) + "\" height=\"" + (maxY + 2 * R + 10) + "\">" +
    edges.join("") + circles + "</svg>";
}

// ---------- benchmarks ----------
let benchChart = null;

async function runBench(kind) {
  const sizes = document.getElementById("bench-sizes").value;
  try {
    const r = await get("/benchmarks/" + kind + "?sizes=" + encodeURIComponent(sizes));
    renderChart(r);
  } catch (e) {
    document.getElementById("bench-table").textContent = "Ошибка: " + e;
  }
}

function renderChart(r) {
  const ctx = document.getElementById("bench-chart").getContext("2d");
  if (benchChart) benchChart.destroy();

  const customPoints = r.points.map(p => ({ x: p.size, y: p.customMs }));
  const standardPoints = r.points.map(p => ({ x: p.size, y: p.standardMs }));

  benchChart = new Chart(ctx, {
    type: "line",
    data: {
      datasets: [
        {
          label: "Своя структура",
          data: customPoints,
          borderColor: "#dc2626",
          backgroundColor: "rgba(220, 38, 38, 0.15)",
          tension: 0.2,
          pointRadius: 4
        },
        {
          label: "Стандартная .NET",
          data: standardPoints,
          borderColor: "#2563eb",
          backgroundColor: "rgba(37, 99, 235, 0.15)",
          tension: 0.2,
          pointRadius: 4
        }
      ]
    },
    options: {
      parsing: false,
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: "nearest", intersect: false },
      scales: {
        x: {
          type: "logarithmic",
          title: { display: true, text: "Размер входа (N)" }
        },
        y: {
          type: "logarithmic",
          title: { display: true, text: "Время, мс" }
        }
      },
      plugins: {
        legend: { position: "top" },
        tooltip: {
          callbacks: {
            title: ctxs => "N = " + ctxs[0].parsed.x,
            afterBody: ctxs => {
              const i = ctxs[0].dataIndex;
              const p = r.points[i];
              return "Ratio (своя / станд.): " + p.ratio.toFixed(2) + "x";
            }
          }
        }
      }
    }
  });

  const rows = r.points.map(p =>
    "<tr><td>" + p.size + "</td><td>" + p.customMs.toFixed(4) + "</td><td>" +
    p.standardMs.toFixed(4) + "</td><td>" + p.ratio.toFixed(2) + "</td></tr>"
  ).join("");
  document.getElementById("bench-table").innerHTML =
    "<div class=\"timing\">" + esc(r.structure) + " · " + esc(r.operation) + "</div>" +
    "<table class=\"trace\"><thead><tr><th>N</th><th>Своя, мс</th><th>Стандартная, мс</th><th>Ratio</th></tr></thead><tbody>" + rows + "</tbody></table>";
}

document.getElementById("bench-stack").addEventListener("click", () => runBench("stacks"));
document.getElementById("bench-queue").addEventListener("click", () => runBench("queues"));
'@

# ---------- 6. docs/REPORT.md ----------
Write-Step "docs/REPORT.md"

Write-File "docs/REPORT.md" @'
# Отчёт по практической работе 3

## 1. Цель

Познакомиться со структурами данных Стек, Очередь, Список, Дерево.
Реализовать их на своём связном списке, сравнить со стандартными
коллекциями .NET по времени выполнения.

## 2. Часть 1. Стек

- Реализация: `LabApp/Core/Stack.cs` (`LinkedListStack<T>`)
- Своя база: `SinglyLinkedList<T>`
- Адаптер к стандартному: `StandardStackAdapter<T>` (обёртка `System.Collections.Generic.Stack<T>`)
- Операции: Push, Pop, Top, IsEmpty, Print
- Парсер `input.txt` в `InputParser.cs`
- Пошаговый trace: `ScenarioRunner` + `TraceEvent`
- Замеры: `Timing` (read/parse/execute)

Пример: `3 4 1,56 1,7 1,cat 2 5 4`

## 3. Часть 1.4. Выражения

- Постфиксное вычисление: `PostfixEvaluator.cs`, O(n) по времени, O(n) по памяти
- Инфикс → постфикс: `ShuntingYard.cs`, O(n), с поддержкой приоритетов, правоассоциативного `^`, функций `ln/cos/sin/sqrt`
- Ошибки: деление на 0, домен ln/sqrt, несбалансированные скобки

## 4. Часть 2. Очередь

- Своя реализация: `ListQueue<T>` на своём связном списке (head/tail)
- Стандартная: `StandardQueueAdapter<T>` над `System.Collections.Generic.Queue<T>`
- Один и тот же сценарий прогоняется на обеих реализациях
- Замеры сравниваются на графике

## 5. Часть 3. Примеры применения структур

- Список: ____
- Стек: ____
- Очередь: ____
- Дерево: ____

Каждый пример — с описанием задачи, реализацией и trace.

## 6. Часть 4. Связные списки

Реализованы 12 алгоритмов в `LabApp/Core/LinkedListAlgorithms.cs`:

1. Reverse
2. MoveLastToFront
3. MoveFirstToEnd
4. CountDistinct
5. RemoveNonUnique
6. InsertSelfAfterX
7. SortedInsert
8. RemoveAll
9. InsertBefore
10. Append
11. SplitByValue
12. Double / Swap

## 7. Дерево

- Парсер префиксной записи: `PrefixParser.cs` (`*` — null-маркер)
- Обходы: preorder/inorder/postorder + высота
- Визуализация в UI

## 8. Сравнение со стандартными структурами

Замеры на наборах:

| Структура | N=100 | N=1000 | N=10000 | N=100000 |
|-----------|-------|--------|---------|----------|
| Stack своя, мс | | | | |
| Stack стан., мс | | | | |
| Queue своя, мс | | | | |
| Queue стан., мс | | | | |

График «время vs размер» строится в веб-интерфейсе,
вкладка Benchmarks. Оси логарифмические.

Вывод: ____

## 9. Как запустить

    dotnet run --project LabApp
    открыть http://localhost:5000

## 10. Выводы

____
'@

# ---------- 7. Сборка ----------
Write-Step "Сборка"
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build упал (код $LASTEXITCODE)." }

# ---------- 8. Тесты ----------
Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet test упал (код $LASTEXITCODE)." }

# ---------- 9. Итог ----------
Write-Step "Готово"
Write-Host "  Проверь в браузере:"
Write-Host "    dotnet run --project LabApp"
Write-Host "    http://localhost:5000"
Write-Host ""
Write-Host "  Вкладки:"
Write-Host "    Stack, Queue  — прогон input.txt с trace и таймингами"
Write-Host "    Expressions   — постфикс + инфикс->постфикс"
Write-Host "    Lists         — 12 алгоритмов списка"
Write-Host "    Tree          — парсинг префикса + SVG-дерево"
Write-Host "    Benchmarks    — график 'время vs размер' для стека и очереди"
Write-Host ""
Write-Host "  Отчёт: docs/REPORT.md"
Write-Host ""
Write-Host "  Тестов было 59, должно остаться 59 (без новых тестов на фронт)."