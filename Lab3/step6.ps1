<#
.SYNOPSIS
    Шаг 6: красивый график бенчмарков (линейные оси, точки + линия).
.DESCRIPTION
    Обновляет Endpoints.cs (поддержка max/step), BenchmarkRunner.cs (больше итераций),
    app.js (Chart.js: линейные оси, стиль как на эталоне). Тесты не трогаем.
#>
[CmdletBinding()]
param([string]$Root = ".")

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Info([string]$msg) { Write-Host "  $msg" }
function Write-File([string]$path, [string]$content) {
    $full = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $path))
    $dir = [System.IO.Path]::GetDirectoryName($full)
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($full, $content, [System.Text.UTF8Encoding]::new($true))
    Write-Info "+ $path"
}

Set-Location (Resolve-Path $Root).Path

# ---------- 1. Обновляем BenchmarkRunner ----------
Write-Step "BenchmarkRunner: больше итераций"
$runner = "LabApp/Core/BenchmarkRunner.cs"
$c = [System.IO.File]::ReadAllText($runner, [System.Text.Encoding]::UTF8)
$c = $c -replace 'int warmupRuns = 3,\s*int iterations = 7,', 'int warmupRuns = 5, int iterations = 20,'
$c = $c -replace 'int warmupRuns = 2,\s*int iterations = 5\)', 'int warmupRuns = 5, int iterations = 20)'
[System.IO.File]::WriteAllText($runner, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "BenchmarkRunner обновлён (warmup=5, iterations=20)"

# ---------- 2. Обновляем Endpoints ----------
Write-Step "Endpoints: параметры max и step"
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
        new(r.Trace.Select(t => new TraceDto(t.Index, t.Operation, t.Argument, t.Result)).ToList(),
            r.FinalState,
            new TimingDto(r.Timing.ReadMs, r.Timing.ParseMs, r.Timing.ExecuteMs, r.Timing.TotalMs));

    private static IResult EvaluatePostfix(ExpressionRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Expression)) return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes) return Results.BadRequest(new { error = "expression too large" });
        var tokens = Tokenizer.Tokenize(req.Expression);
        if (!tokens.IsSuccess) return Results.Ok(new EvalResponse(false, null, tokens.Error));
        var value = PostfixEvaluator.Evaluate(tokens.Value);
        return Results.Ok(value.IsSuccess ? new EvalResponse(true, value.Value, null) : new EvalResponse(false, null, value.Error));
    }

    private static IResult InfixToPostfix(ExpressionRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Expression)) return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes) return Results.BadRequest(new { error = "expression too large" });
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
                case "reverse": LinkedListAlgorithms.Reverse(list); return Ok(before, list.Items.ToArray(), null);
                case "moveLastToFront": LinkedListAlgorithms.MoveLastToFront(list); return Ok(before, list.Items.ToArray(), null);
                case "moveFirstToEnd": LinkedListAlgorithms.MoveFirstToEnd(list); return Ok(before, list.Items.ToArray(), null);
                case "countDistinct": return Ok(before, list.Items.ToArray(), new { count = LinkedListAlgorithms.CountDistinct(list) });
                case "removeNonUnique": LinkedListAlgorithms.RemoveNonUnique(list); return Ok(before, list.Items.ToArray(), null);
                case "insertSelfAfterX":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (x) required" });
                    LinkedListAlgorithms.InsertSelfAfterX(list, req.Arg.Value); return Ok(before, list.Items.ToArray(), null);
                case "sortedInsert":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (e) required" });
                    LinkedListAlgorithms.SortedInsert(list, req.Arg.Value); return Ok(before, list.Items.ToArray(), null);
                case "removeAll":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (e) required" });
                    LinkedListAlgorithms.RemoveAll(list, req.Arg.Value); return Ok(before, list.Items.ToArray(), null);
                case "insertBefore":
                    if (req.Arg is null || req.Arg2 is null) return Results.BadRequest(new { error = "arg (e) and arg2 (f) required" });
                    LinkedListAlgorithms.InsertBefore(list, req.Arg.Value, req.Arg2.Value); return Ok(before, list.Items.ToArray(), null);
                case "append":
                    if (req.SecondList is null) return Results.BadRequest(new { error = "secondList required" });
                    var second = new SinglyLinkedList<int>();
                    foreach (var v in req.SecondList) second.AddLast(v);
                    LinkedListAlgorithms.Append(list, second); return Ok(before, list.Items.ToArray(), null);
                case "splitByValue":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (x) required" });
                    var (left, right) = LinkedListAlgorithms.SplitByValue(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), new { left = left.Items.ToArray(), right = right.Items.ToArray() });
                case "double": LinkedListAlgorithms.Double(list); return Ok(before, list.Items.ToArray(), null);
                case "swap":
                    if (req.Arg is null || req.Arg2 is null) return Results.BadRequest(new { error = "arg (a) and arg2 (b) required" });
                    var swapped = LinkedListAlgorithms.Swap(list, req.Arg.Value, req.Arg2.Value);
                    return Ok(before, list.Items.ToArray(), new { swapped });
                default: return Results.BadRequest(new { error = $"unknown operation: {op}" });
            }
        }
        catch (Exception ex) { return Results.Ok(new ListResponse(false, ex.Message, before, null, null)); }
    }

    private static IResult Ok(int[] before, int[] after, object? result) => Results.Ok(new ListResponse(true, null, before, after, result));

    private static IResult ParsePrefix(TreeRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Expression)) return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes) return Results.BadRequest(new { error = "expression too large" });
        var tree = PrefixParser.ParsePrefix(req.Expression);
        if (tree.Root is null) return Results.Ok(new TreeResponse(true, null, null, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), 0));
        var pre = TreeTraversal.PreOrder(tree.Root).Select(c => (int)c).ToArray();
        var ino = TreeTraversal.InOrder(tree.Root).Select(c => (int)c).ToArray();
        var post = TreeTraversal.PostOrder(tree.Root).Select(c => (int)c).ToArray();
        return Results.Ok(new TreeResponse(true, null, ToNodeDto(tree.Root), pre, ino, post, TreeTraversal.Height(tree.Root)));
    }

    private static TreeNodeDto? ToNodeDto<T>(TreeNode<T>? node) => node is null ? null : new TreeNodeDto(node.Value?.ToString() ?? "", ToNodeDto(node.Left), ToNodeDto(node.Right));

    // ----- Бенчмарки (обновлено) -----
    private static IResult BenchStacks(string? sizes, int max = 100000, int step = 2500)
    {
        var parsed = ParseSizes(sizes, max, step);
        if (parsed is null) return Results.BadRequest(new { error = "invalid sizes" });
        var result = BenchmarkRunner.CompareStacks(parsed, warmupRuns: 5, iterations: 20);
        return Results.Ok(ToBenchResponse(result));
    }

    private static IResult BenchQueues(string? sizes, int max = 100000, int step = 2500)
    {
        var parsed = ParseSizes(sizes, max, step);
        if (parsed is null) return Results.BadRequest(new { error = "invalid sizes" });
        var result = BenchmarkRunner.CompareQueues(parsed, warmupRuns: 5, iterations: 20);
        return Results.Ok(ToBenchResponse(result));
    }

    private static int[]? ParseSizes(string? raw, int max, int step)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var res = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out var v) || v <= 0 || v > MaxBenchmarkSize) return null;
                res[i] = v;
            }
            return res;
        }
        if (max <= 0 || max > MaxBenchmarkSize || step <= 0 || step > max) return null;
        var list = new List<int>();
        for (int i = step; i <= max; i += step) list.Add(i);
        return list.ToArray();
    }

    private static BenchmarkResponse ToBenchResponse(BenchmarkResult r) =>
        new(r.Structure, r.Operation, r.Points.Select(p => new BenchmarkPointDto(p.Size, p.CustomMs, p.StandardMs, p.Ratio)).ToList());
}
'@

# ---------- 3. Обновляем app.js (только chart + запросы) ----------
Write-Step "app.js: стилизация графика"
$jsPath = "LabApp/wwwroot/app.js"
$c = [System.IO.File]::ReadAllText($jsPath, [System.Text.Encoding]::UTF8)

# Заменяем целиком функцию runBench и renderChart
$pattern = '(?s)async function runBench\(kind\).*?document\.getElementById\("bench-queue"\)\.addEventListener\("click", \(\) => runBench\("queues"\)\);'
$replacement = @'
async function runBench(kind) {
  const max = document.getElementById("bench-max").value;
  const step = document.getElementById("bench-step").value;
  const out = document.getElementById("bench-table");
  out.textContent = "Считаем (это может занять до 10 секунд)...";
  try {
    const r = await get("/benchmarks/" + kind + "?max=" + max + "&step=" + step);
    renderChart(r);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
}

function renderChart(r) {
  const ctx = document.getElementById("bench-chart").getContext("2d");
  if (benchChart) benchChart.destroy();

  const customData = r.points.map(p => ({ x: p.size, y: p.customMs }));
  const standardData = r.points.map(p => ({ x: p.size, y: p.standardMs }));

  // Опция для 1:1 или квадратного аспекта (по желанию можно поменять 1.5 на 1)
  const aspect = 1.6;

  benchChart = new Chart(ctx, {
    type: "scatter", // base type
    data: {
      datasets: [
        {
          label: "Своя структура (эмпирика)",
          data: customData,
          borderColor: "rgba(0, 188, 212, 0.8)",
          backgroundColor: "rgba(0, 188, 212, 0.8)",
          pointRadius: 3,
          pointHoverRadius: 5,
          showLine: false,
          order: 1
        },
        {
          label: "Стандартная .NET (аппроксимация)",
          data: standardData,
          borderColor: "#ff9800",
          backgroundColor: "#ff9800",
          pointRadius: 0,
          borderWidth: 2,
          showLine: true,
          tension: 0,
          order: 0
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: true,
      aspectRatio: aspect,
      interaction: { mode: "nearest", intersect: false },
      scales: {
        x: {
          type: "linear",
          beginAtZero: true,
          title: { display: true, text: "Размер входных данных (N)" },
          grid: { color: "#e5e7eb", drawBorder: false },
          ticks: { callback: (v) => v.toLocaleString() }
        },
        y: {
          type: "linear",
          beginAtZero: true,
          title: { display: true, text: "Время (мс)" },
          grid: { color: "#e5e7eb", drawBorder: false },
          ticks: { callback: (v) => v.toFixed(2) }
        }
      },
      plugins: {
        legend: {
          position: "bottom",
          align: "end",
          labels: {
            usePointStyle: true,
            boxWidth: 10,
            padding: 20
          }
        },
        tooltip: {
          callbacks: {
            title: (ctxs) => "N = " + ctxs[0].parsed.x.toLocaleString(),
            afterBody: (ctxs) => {
              const i = ctxs[0].dataIndex;
              const p = r.points[i];
              if (!p) return "";
              return "Ratio (своя / станд.): " + p.ratio.toFixed(2) + "x";
            }
          }
        }
      }
    }
  });

  const rows = r.points.map(p =>
    "<tr><td>" + p.size.toLocaleString() + "</td><td>" + p.customMs.toFixed(4) + "</td><td>" +
    p.standardMs.toFixed(4) + "</td><td>" + p.ratio.toFixed(2) + "</td></tr>"
  ).join("");
  out.innerHTML =
    "<div class=\"timing\">" + esc(r.structure) + " · " + esc(r.operation) + "</div>" +
    "<table class=\"trace\"><thead><tr><th>N</th><th>Своя, мс</th><th>Стандартная, мс</th><th>Ratio</th></tr></thead><tbody>" + rows + "</tbody></table>";
}

document.getElementById("bench-stack").addEventListener("click", () => runBench("stacks"));
document.getElementById("bench-queue").addEventListener("click", () => runBench("queues"));
'@

$c = [regex]::Replace($c, $pattern, $replacement, [System.Text.RegularExpressions.RegexOptions]::Singleline)
[System.IO.File]::WriteAllText($jsPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "app.js: chart.js переписан"

# ---------- 4. index.html: добавляем поля max / step ----------
Write-Step "index.html: поля max / step"
$htmlPath = "LabApp/wwwroot/index.html"
$c = [System.IO.File]::ReadAllText($htmlPath, [System.Text.Encoding]::UTF8)

$c = $c -replace '<label>Размеры входа \(через запятую\): <input id="bench-sizes" type="text" value="100,1000,10000,100000"></label>', @'
<div class="row">
  <label>Максимальный N: <input id="bench-max" type="number" value="100000" style="min-width:120px"></label>
  <label>Шаг: <input id="bench-step" type="number" value="2500" style="min-width:100px"></label>
</div>
'@

[System.IO.File]::WriteAllText($htmlPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "index.html: поля max и step"

# ---------- 5. Сборка и тесты ----------
Write-Step "Сборка"
$sln = if (Test-Path "LabApp.slnx") { "LabApp.slnx" } else { "LabApp.sln" }
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build упал" }

Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet test упал" }

Write-Step "Готово"
Write-Host "  Запускай: dotnet run --project LabApp"
Write-Host "  Открой http://localhost:5000 -> Benchmarks"
Write-Host ""
Write-Host "  Что изменилось:"
Write-Host "    - линейные оси, начинаются с нуля"
Write-Host "    - своя структура: голубые точки (эмпирика)"
Write-Host "    - стандартная .NET: оранжевая линия (базовая/аппроксимация)"
Write-Host "    - количество точек регулируется: max и step"
Write-Host "    - warmup 5, итераций 20 (график гладкий)"
Write-Host "    - аспект 1.6 (широкий, как у тебя на картинке)"