<#
.SYNOPSIS
    Scaffold для практической работы 3 (LabApp) под .NET 10 / .slnx.
.DESCRIPTION
    Создаёт решение (.slnx или .sln — по тому, что сгенерирует SDK),
    Web-проект, тестовый проект, исходные структуры, тесты, README, ADR.
    Все файлы пишутся в UTF-8 with BOM — для совместимости с Windows PowerShell 5.1.
    Без git-команд.
.PARAMETER Root
    Рабочая папка. По умолчанию — текущая.
.PARAMETER Force
    Удалить существующие LabApp/, Tests/, LabApp.sln* и пересоздать всё с нуля.
#>
[CmdletBinding()]
param(
    [string]$Root = ".",
    [switch]$Force
)

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

# ---------- 1. Проверка окружения ----------
Write-Step "Проверка окружения"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK не найден. Установи .NET 10 SDK: https://dotnet.microsoft.com/download"
}
$dotnetVersion = (dotnet --version).Trim()
Write-Info "dotnet $dotnetVersion"

if (-not $dotnetVersion.StartsWith("10.")) {
    Write-Host "  Внимание: ожидался SDK 10.x, обнаружен $dotnetVersion" -ForegroundColor Yellow
    Write-Host "  Скрипт рассчитан на .NET 10, но продолжит работу." -ForegroundColor Yellow
}

# ---------- 2. Рабочая папка ----------
Write-Step "Рабочая папка"
$rootPath = (Resolve-Path $Root).Path
Set-Location $rootPath
Write-Info $rootPath

# ---------- 3. Safety / cleanup ----------
Write-Step "Проверка чистоты папки"
$clutter = @()
foreach ($p in @("LabApp", "Tests", "LabApp.slnx", "LabApp.sln")) {
    if (Test-Path $p) { $clutter += $p }
}

if ($clutter.Count -gt 0) {
    if (-not $Force) {
        Write-Host "  Найдены существующие артефакты:" -ForegroundColor Yellow
        foreach ($p in $clutter) { Write-Host "    - $p" -ForegroundColor Yellow }
        throw "Папка не пуста. Запусти с -Force, чтобы удалить и пересоздать, или удали эти файлы вручную."
    }
    Write-Info "удаляю старые артефакты (-Force)"
    foreach ($p in $clutter) {
        Remove-Item $p -Recurse -Force
        Write-Info "  удалён $p"
    }
}

# ---------- 4. global.json ----------
Write-Step "global.json"
Write-File "global.json" @'
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMajor",
    "allowPrerelease": false
  }
}
'@

# ---------- 5. Решение ----------
Write-Step "Создание решения"
dotnet new sln -n LabApp
if ($LASTEXITCODE -ne 0) { throw "dotnet new sln завершился с ошибкой (код $LASTEXITCODE)" }

$sln = $null
if (Test-Path "LabApp.slnx")      { $sln = "LabApp.slnx" }
elseif (Test-Path "LabApp.sln")   { $sln = "LabApp.sln" }
else { throw "Не удалось определить solution-файл: ни LabApp.slnx, ни LabApp.sln не появились" }
Write-Info "solution: $sln"

# ---------- 6. Web-проект ----------
Write-Step "Web-проект LabApp"
dotnet new web -n LabApp -o LabApp
if ($LASTEXITCODE -ne 0) { throw "dotnet new web завершился с ошибкой (код $LASTEXITCODE)" }
Write-Info "LabApp/LabApp.csproj создан"

# ---------- 7. Папки ----------
Write-Step "Структура папок"
foreach ($d in @("LabApp/Core", "LabApp/Api", "LabApp/wwwroot", "docs")) {
    if (-not (Test-Path $d)) {
        New-Item -ItemType Directory -Path $d -Force | Out-Null
        Write-Info "+ $d/"
    }
}

# ---------- 8. Тестовый проект ----------
Write-Step "Тестовый проект"
dotnet new xunit -n LabApp.Tests -o Tests/LabApp.Tests
if ($LASTEXITCODE -ne 0) { throw "dotnet new xunit завершился с ошибкой (код $LASTEXITCODE)" }
Remove-Item "Tests/LabApp.Tests/UnitTest1.cs" -ErrorAction SilentlyContinue
Write-Info "Tests/LabApp.Tests создан"

# ---------- 9. Проекты в решение ----------
Write-Step "Проекты -> решение"
dotnet sln $sln add "LabApp\LabApp.csproj"
if ($LASTEXITCODE -ne 0) { throw "не удалось добавить LabApp в $sln" }
dotnet sln $sln add "Tests\LabApp.Tests\LabApp.Tests.csproj"
if ($LASTEXITCODE -ne 0) { throw "не удалось добавить Tests в $sln" }
Write-Info "готово"

# ---------- 10. Ссылка Tests -> LabApp ----------
Write-Step "Ссылка Tests -> LabApp"
dotnet add "Tests\LabApp.Tests\LabApp.Tests.csproj" reference "LabApp\LabApp.csproj"
if ($LASTEXITCODE -ne 0) { throw "не удалось добавить project reference" }
Write-Info "готова"

# ---------- 11. Файлы ----------
Write-Step "Запись исходных файлов"

# .gitignore
Write-File ".gitignore" @'
bin/
obj/
.vs/
.vscode/
.idea/
*.user
*.suo
[Dd]ebug/
[Rr]elease/
TestResults/
[Tt]est[Rr]esults/
'@

# README.md
Write-File "README.md" @'
# LabApp — практическая работа 3

Единое веб-приложение по структурам данных: стек, очередь, выражения,
связные списки, дерево. Со сравнением своих структур со стандартными
коллекциями .NET и графиком «время от размера входа».

## Стек технологий

- .NET 10 (LTS)
- ASP.NET Core Minimal API
- xUnit v3
- Frontend: один index.html + Chart.js (на следующих шагах)
- Solution: LabApp.slnx

## Что реализовано

- Стек на своём связном списке + адаптер к стандартному Stack<T>
- Очередь на своём связном списке + адаптер к стандартному Queue<T>
- 12 алгоритмов связного списка
- Бинарное дерево из префиксной записи с маркером null
- (в следующих шагах) парсер input.txt, выражения, бенчмарки

## Быстрый старт

    dotnet restore
    dotnet run --project LabApp

Открыть в браузере http://localhost:5000

## Тесты

    dotnet test

## Структура

- LabApp/Core — доменные структуры и алгоритмы
- LabApp/Api — Minimal API эндпоинты (появится на шаге 3)
- LabApp/wwwroot — фронтенд
- Tests/LabApp.Tests — xUnit тесты
- docs — ADR и отчёт

## Документация

- docs/ADR.md — ключевые архитектурные решения
- docs/REPORT.md — отчёт по практической (будет добавлен)
'@

# docs/ADR.md
Write-File "docs/ADR.md" @'
# ADR — Architecture Decision Records

## ADR-0001. Модульный монолит

Статус: принято.
Контекст: учебный домен, одна команда, ограниченное время.
Решение: один процесс ASP.NET Core, модули — папками (Core, Api). Микросервисы избыточны.
Следствия: проще отладка и деплой; границы держим код-ревью.

## ADR-0002. Свои структуры в Core, стандартные — через адаптеры

Статус: принято.
Контекст: нужны и свои Stack/Queue, и стандартные BCL-коллекции для сравнения.
Решение: свои реализации в Core, стандартные подключаются адаптерами к тем же портам.
Следствия: домен не знает про BCL, сравнение симметрично.

## ADR-0003. Batch-first исполнение

Статус: принято.
Контекст: основной сценарий по заданию — прочитать input.txt и вывести trace.
Решение: основной режим — POST /runs, stateless. Интерактив — дополнительный.
Следствия: API горизонтально масштабируется, прогоны воспроизводимы.

## ADR-0004. Выражения — только через собственный парсер

Статус: принято.
Контекст: нельзя использовать eval, нужен контроль ошибок и сложность.
Решение: Tokenizer + PostfixEvaluator + ShuntingYard. Ошибки типизированы.
Следствия: безопасно, предсказуемо, O(n).

## ADR-0005. Сравнение через общий порт

Статус: принято.
Контекст: если свою структуру звать через интерфейс, а стандартную напрямую —
разница во времени частично объясняется виртуальными вызовами.
Решение: в сравнении обе серии идут через один и тот же адаптер.
Следствия: график показывает разницу структур, а не API.

## ADR-0006. Сборка под .NET 10 и .slnx

Статус: принято.
Контекст: в .NET 10 дефолтный формат решения — .slnx (XML), а C# 14 ужесточил
nullable-анализ для unconstrained generic (запрет на ?.Value ?? throw).
Решение: используем .slnx по умолчанию, все проверки на null — явные
(is null -> return/throw), без ?. и ?? для generic T.
Следствия: совместимость с новыми SDK без сюрпризов.
'@

# Program.cs
Write-File "LabApp/Program.cs" @'
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

app.Run();
'@

# index.html
Write-File "LabApp/wwwroot/index.html" @'
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>LabApp</title>
<style>
  body { font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
         max-width: 820px; margin: 2rem auto; padding: 0 1rem; color: #222; }
  h1 { margin-top: 0; }
  a { color: #0b6bcb; }
  .card { border: 1px solid #e3e3e3; border-radius: 10px; padding: 1rem 1.25rem;
          margin: 1rem 0; }
  .muted { color: #666; }
</style>
</head>
<body>
<h1>LabApp</h1>
<p>Практическая работа 3. Скелет развёрнут и работает.</p>

<div class="card">
  <strong>Проверка API</strong>
  <p><a href="/api/health">/api/health</a> — должен вернуть status ok.</p>
</div>

<div class="card">
  <strong>Что дальше</strong>
  <ul>
    <li>Шаг 2: парсер input.txt, ScenarioRunner, выражения</li>
    <li>Шаг 3: Minimal API эндпоинты</li>
    <li>Шаг 4: фронтенд с Chart.js (сравнение структур)</li>
    <li>Шаг 5: бенчмарки и график «время от размера»</li>
    <li>Шаг 6: ADR, отчёт, сдача</li>
  </ul>
</div>

<p class="muted">Если видишь эту страницу — статика и API работают.</p>
</body>
</html>
'@

# Core/ListNode.cs
Write-File "LabApp/Core/ListNode.cs" @'
namespace LabApp.Core;

public sealed class ListNode<T>
{
    public T Value { get; set; }
    public ListNode<T>? Next { get; set; }
    public ListNode(T value) => Value = value;
}
'@

# Core/SinglyLinkedList.cs
Write-File "LabApp/Core/SinglyLinkedList.cs" @'
namespace LabApp.Core;

public sealed class SinglyLinkedList<T>
{
    public ListNode<T>? Head { get; private set; }
    public ListNode<T>? Tail { get; private set; }
    public int Count { get; private set; }
    public bool IsEmpty => Head is null;

    public void AddFirst(T value)
    {
        var node = new ListNode<T>(value) { Next = Head };
        Head = node;
        Tail ??= node;
        Count++;
    }

    public void AddLast(T value)
    {
        var node = new ListNode<T>(value);
        if (Tail is null) { Head = Tail = node; }
        else { Tail.Next = node; Tail = node; }
        Count++;
    }

    public T RemoveFirst()
    {
        if (Head is null) throw new InvalidOperationException("Список пуст");
        var v = Head.Value;
        Head = Head.Next;
        if (Head is null) Tail = null;
        Count--;
        return v;
    }

    public T RemoveLast()
    {
        if (Head is null) throw new InvalidOperationException("Список пуст");
        if (Head == Tail) { var v0 = Head.Value; Head = Tail = null; Count--; return v0; }
        var cur = Head;
        while (cur!.Next != Tail) cur = cur.Next;
        var v = Tail!.Value;
        cur.Next = null;
        Tail = cur;
        Count--;
        return v;
    }

    public void SetHeadOnly(ListNode<T>? head) => Head = head;

    public void RecomputeTailAndCount()
    {
        Tail = null;
        int c = 0;
        for (var cur = Head; cur is not null; cur = cur.Next) { Tail = cur; c++; }
        Count = c;
    }

    public IEnumerable<T> Items
    {
        get
        {
            for (var cur = Head; cur is not null; cur = cur.Next) yield return cur.Value;
        }
    }

    public SinglyLinkedList<T> Clone()
    {
        var copy = new SinglyLinkedList<T>();
        foreach (var v in Items) copy.AddLast(v);
        return copy;
    }

    public override string ToString() => "[" + string.Join(", ", Items) + "]";
}
'@

# Core/LinkedListAlgorithms.cs
Write-File "LabApp/Core/LinkedListAlgorithms.cs" @'
namespace LabApp.Core;

public static class LinkedListAlgorithms
{
    public static void Reverse(SinglyLinkedList<int> list)
    {
        ListNode<int>? prev = null, cur = list.Head;
        while (cur is not null)
        {
            var nxt = cur.Next;
            cur.Next = prev;
            prev = cur;
            cur = nxt;
        }
        list.SetHeadOnly(prev);
        list.RecomputeTailAndCount();
    }

    public static void MoveLastToFront(SinglyLinkedList<int> list)
    {
        if (list.Head is null || list.Head == list.Tail) return;
        var last = list.RemoveLast();
        list.AddFirst(last);
    }

    public static void MoveFirstToEnd(SinglyLinkedList<int> list)
    {
        if (list.Head is null || list.Head == list.Tail) return;
        var first = list.RemoveFirst();
        list.AddLast(first);
    }

    public static int CountDistinct(SinglyLinkedList<int> list)
        => list.Items.Distinct().Count();

    public static void RemoveNonUnique(SinglyLinkedList<int> list)
    {
        var counts = list.Items.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        RemoveWhere(list, x => counts[x] > 1);
    }

    public static void InsertSelfAfterX(SinglyLinkedList<int> list, int x)
    {
        var cur = list.Head;
        while (cur is not null && cur.Value != x) cur = cur.Next;
        if (cur is null) return;

        var snapshot = list.Items.ToList();
        var insertAt = cur;
        foreach (var v in snapshot)
        {
            var node = new ListNode<int>(v) { Next = insertAt.Next };
            insertAt.Next = node;
            insertAt = node;
        }
        list.RecomputeTailAndCount();
    }

    public static void SortedInsert(SinglyLinkedList<int> list, int e)
    {
        if (list.Head is null || e <= list.Head.Value) { list.AddFirst(e); return; }
        var cur = list.Head;
        while (cur.Next is not null && cur.Next.Value < e) cur = cur.Next;
        var node = new ListNode<int>(e) { Next = cur.Next };
        cur.Next = node;
        list.RecomputeTailAndCount();
    }

    public static void RemoveAll(SinglyLinkedList<int> list, int e)
        => RemoveWhere(list, x => x == e);

    public static void InsertBefore(SinglyLinkedList<int> list, int e, int f)
    {
        if (list.Head is null) return;
        if (list.Head.Value == e) { list.AddFirst(f); return; }
        var cur = list.Head;
        while (cur.Next is not null && cur.Next.Value != e) cur = cur.Next;
        if (cur.Next is null) return;
        var node = new ListNode<int>(f) { Next = cur.Next };
        cur.Next = node;
        list.RecomputeTailAndCount();
    }

    public static void Append(SinglyLinkedList<int> l, SinglyLinkedList<int> e)
    {
        foreach (var v in e.Items.ToList()) l.AddLast(v);
    }

    public static (SinglyLinkedList<int> left, SinglyLinkedList<int> right) SplitByValue(
        SinglyLinkedList<int> list, int x)
    {
        var left = new SinglyLinkedList<int>();
        var right = new SinglyLinkedList<int>();
        var found = false;
        foreach (var v in list.Items)
        {
            if (!found && v == x) { found = true; right.AddLast(v); }
            else if (found) right.AddLast(v);
            else left.AddLast(v);
        }
        return (left, right);
    }

    public static void Double(SinglyLinkedList<int> list)
    {
        var snapshot = list.Items.ToList();
        foreach (var v in snapshot) list.AddLast(v);
    }

    public static bool Swap(SinglyLinkedList<int> list, int a, int b)
    {
        if (a == b) return false;

        ListNode<int>? pa = null, pb = null, prevA = null, prevB = null, prev = null;
        for (var cur = list.Head; cur is not null; cur = cur.Next)
        {
            if (cur.Value == a && pa is null) { pa = cur; prevA = prev; }
            if (cur.Value == b && pb is null) { pb = cur; prevB = prev; }
            prev = cur;
        }
        if (pa is null || pb is null) return false;

        if (pa.Next == pb)
        {
            if (prevA is not null) prevA.Next = pb; else list.SetHeadOnly(pb);
            pa.Next = pb.Next;
            pb.Next = pa;
        }
        else if (pb.Next == pa)
        {
            if (prevB is not null) prevB.Next = pa; else list.SetHeadOnly(pa);
            pb.Next = pa.Next;
            pa.Next = pb;
        }
        else
        {
            if (prevA is not null) prevA.Next = pb; else list.SetHeadOnly(pb);
            if (prevB is not null) prevB.Next = pa; else list.SetHeadOnly(pa);
            var tmp = pa.Next;
            pa.Next = pb.Next;
            pb.Next = tmp;
        }
        list.RecomputeTailAndCount();
        return true;
    }

    private static void RemoveWhere(SinglyLinkedList<int> list, Func<int, bool> pred)
    {
        while (list.Head is not null && pred(list.Head.Value))
        {
            list.SetHeadOnly(list.Head.Next);
        }
        var cur = list.Head;
        while (cur?.Next is not null)
        {
            if (pred(cur.Next.Value)) cur.Next = cur.Next.Next;
            else cur = cur.Next;
        }
        list.RecomputeTailAndCount();
    }
}
'@

# Core/Stack.cs — с явной null-проверкой, никаких ?.Value ?? throw для generic T
Write-File "LabApp/Core/Stack.cs" @'
namespace LabApp.Core;

public interface IStack<T>
{
    int Count { get; }
    void Push(T value);
    T Pop();
    T Top();
    bool IsEmpty();
    IReadOnlyList<T> Print();
}

public sealed class LinkedListStack<T> : IStack<T>
{
    private readonly SinglyLinkedList<T> _inner = new();
    public int Count => _inner.Count;
    public void Push(T value) => _inner.AddFirst(value);
    public T Pop() => _inner.RemoveFirst();
    public T Top()
    {
        var head = _inner.Head;
        if (head is null) throw new InvalidOperationException("Стек пуст");
        return head.Value;
    }
    public bool IsEmpty() => _inner.IsEmpty;
    public IReadOnlyList<T> Print() => _inner.Items.ToList();
}

public sealed class StandardStackAdapter<T> : IStack<T>
{
    private readonly System.Collections.Generic.Stack<T> _inner = new();
    public int Count => _inner.Count;
    public void Push(T value) => _inner.Push(value);
    public T Pop() => _inner.Pop();
    public T Top() => _inner.Peek();
    public bool IsEmpty() => _inner.Count == 0;
    public IReadOnlyList<T> Print() => _inner.ToList();
}
'@

# Core/Queue.cs — аналогично
Write-File "LabApp/Core/Queue.cs" @'
namespace LabApp.Core;

public interface IQueue<T>
{
    int Count { get; }
    void Enqueue(T value);
    T Dequeue();
    T Peek();
    bool IsEmpty();
    IReadOnlyList<T> Print();
}

public sealed class ListQueue<T> : IQueue<T>
{
    private readonly SinglyLinkedList<T> _inner = new();
    public int Count => _inner.Count;
    public void Enqueue(T value) => _inner.AddLast(value);
    public T Dequeue() => _inner.RemoveFirst();
    public T Peek()
    {
        var head = _inner.Head;
        if (head is null) throw new InvalidOperationException("Очередь пуста");
        return head.Value;
    }
    public bool IsEmpty() => _inner.IsEmpty;
    public IReadOnlyList<T> Print() => _inner.Items.ToList();
}

public sealed class StandardQueueAdapter<T> : IQueue<T>
{
    private readonly System.Collections.Generic.Queue<T> _inner = new();
    public int Count => _inner.Count;
    public void Enqueue(T value) => _inner.Enqueue(value);
    public T Dequeue() => _inner.Dequeue();
    public T Peek() => _inner.Peek();
    public bool IsEmpty() => _inner.Count == 0;
    public IReadOnlyList<T> Print() => _inner.ToList();
}
'@

# Core/BinaryTree.cs
Write-File "LabApp/Core/BinaryTree.cs" @'
namespace LabApp.Core;

public sealed class TreeNode<T>
{
    public T Value { get; init; } = default!;
    public TreeNode<T>? Left { get; set; }
    public TreeNode<T>? Right { get; set; }
}

public sealed class BinaryTree<T>
{
    public TreeNode<T>? Root { get; set; }
}
'@

# Core/PrefixParser.cs
Write-File "LabApp/Core/PrefixParser.cs" @'
namespace LabApp.Core;

public static class PrefixParser
{
    public static BinaryTree<char> ParsePrefix(string s)
    {
        int pos = 0;
        var root = Parse(s, ref pos);
        return new BinaryTree<char> { Root = root };
    }

    private static TreeNode<char>? Parse(string s, ref int pos)
    {
        if (pos >= s.Length) return null;
        var c = s[pos++];
        if (c == '*') return null;
        var node = new TreeNode<char> { Value = c };
        node.Left = Parse(s, ref pos);
        node.Right = Parse(s, ref pos);
        return node;
    }
}
'@

# Tests/LinkedListTests.cs
Write-File "Tests/LabApp.Tests/LinkedListTests.cs" @'
using LabApp.Core;
using Xunit;

public class LinkedListTests
{
    private static SinglyLinkedList<int> Make(params int[] xs)
    {
        var l = new SinglyLinkedList<int>();
        foreach (var x in xs) l.AddLast(x);
        return l;
    }

    [Fact] public void Reverse_Works()
    {
        var l = Make(1, 2, 3, 4);
        LinkedListAlgorithms.Reverse(l);
        Assert.Equal(new[] { 4, 3, 2, 1 }, l.Items);
    }

    [Fact] public void Reverse_Empty_Ok()
    {
        var l = new SinglyLinkedList<int>();
        LinkedListAlgorithms.Reverse(l);
        Assert.Empty(l.Items);
    }

    [Fact] public void Reverse_Twice_Identity()
    {
        var l = Make(1, 2, 3);
        LinkedListAlgorithms.Reverse(l);
        LinkedListAlgorithms.Reverse(l);
        Assert.Equal(new[] { 1, 2, 3 }, l.Items);
    }

    [Fact] public void MoveLastToFront_Works()
    {
        var l = Make(1, 2, 3);
        LinkedListAlgorithms.MoveLastToFront(l);
        Assert.Equal(new[] { 3, 1, 2 }, l.Items);
    }

    [Fact] public void CountDistinct_Works()
    {
        Assert.Equal(2, LinkedListAlgorithms.CountDistinct(Make(1, 1, 2, 2, 2, 1)));
    }

    [Fact] public void RemoveNonUnique_LeavesOnlyUniques()
    {
        var l = Make(1, 1, 2, 3, 3, 4);
        LinkedListAlgorithms.RemoveNonUnique(l);
        Assert.Equal(new[] { 2, 4 }, l.Items);
    }

    [Fact] public void RemoveAll_Works()
    {
        var l = Make(1, 2, 1, 3, 1);
        LinkedListAlgorithms.RemoveAll(l, 1);
        Assert.Equal(new[] { 2, 3 }, l.Items);
    }

    [Fact] public void InsertBefore_Works()
    {
        var l = Make(1, 2, 3);
        LinkedListAlgorithms.InsertBefore(l, 2, 99);
        Assert.Equal(new[] { 1, 99, 2, 3 }, l.Items);
    }

    [Fact] public void SortedInsert_KeepsOrder()
    {
        var l = Make(1, 3, 5);
        LinkedListAlgorithms.SortedInsert(l, 4);
        Assert.Equal(new[] { 1, 3, 4, 5 }, l.Items);
    }

    [Fact] public void SplitByValue_Works()
    {
        var (left, right) = LinkedListAlgorithms.SplitByValue(Make(1, 2, 3, 4), 3);
        Assert.Equal(new[] { 1, 2 }, left.Items);
        Assert.Equal(new[] { 3, 4 }, right.Items);
    }

    [Fact] public void SplitByValue_Missing_RightEmpty()
    {
        var (left, right) = LinkedListAlgorithms.SplitByValue(Make(1, 2), 99);
        Assert.Equal(new[] { 1, 2 }, left.Items);
        Assert.Empty(right.Items);
    }

    [Fact] public void Double_Works()
    {
        var l = Make(1, 2);
        LinkedListAlgorithms.Double(l);
        Assert.Equal(new[] { 1, 2, 1, 2 }, l.Items);
    }

    [Fact] public void Append_Works()
    {
        var l = Make(1, 2);
        LinkedListAlgorithms.Append(l, Make(3, 4));
        Assert.Equal(new[] { 1, 2, 3, 4 }, l.Items);
    }

    [Fact] public void Swap_Adjacent()
    {
        var l = Make(1, 2, 3);
        Assert.True(LinkedListAlgorithms.Swap(l, 1, 2));
        Assert.Equal(new[] { 2, 1, 3 }, l.Items);
    }

    [Fact] public void Swap_NonAdjacent()
    {
        var l = Make(1, 2, 3, 4, 5);
        Assert.True(LinkedListAlgorithms.Swap(l, 2, 4));
        Assert.Equal(new[] { 1, 4, 3, 2, 5 }, l.Items);
    }

    [Fact] public void Swap_Missing_ReturnsFalse()
    {
        var l = Make(1, 2, 3);
        Assert.False(LinkedListAlgorithms.Swap(l, 1, 99));
        Assert.Equal(new[] { 1, 2, 3 }, l.Items);
    }
}
'@

# Tests/StackQueueTests.cs
Write-File "Tests/LabApp.Tests/StackQueueTests.cs" @'
using LabApp.Core;
using Xunit;

public class StackQueueTests
{
    [Fact] public void LinkedListStack_LIFO()
    {
        var s = new LinkedListStack<int>();
        s.Push(1); s.Push(2); s.Push(3);
        Assert.Equal(3, s.Pop());
        Assert.Equal(2, s.Top());
        Assert.False(s.IsEmpty());
    }

    [Fact] public void StandardStack_LIFO()
    {
        var s = new StandardStackAdapter<int>();
        s.Push(1); s.Push(2); s.Push(3);
        Assert.Equal(3, s.Pop());
        Assert.Equal(2, s.Top());
    }

    [Fact] public void ListQueue_FIFO()
    {
        var q = new ListQueue<int>();
        q.Enqueue(1); q.Enqueue(2); q.Enqueue(3);
        Assert.Equal(1, q.Dequeue());
        Assert.Equal(2, q.Peek());
    }

    [Fact] public void StandardQueue_FIFO()
    {
        var q = new StandardQueueAdapter<int>();
        q.Enqueue(1); q.Enqueue(2); q.Enqueue(3);
        Assert.Equal(1, q.Dequeue());
        Assert.Equal(2, q.Peek());
    }

    [Fact] public void BothStacks_GiveSameResults()
    {
        var a = new LinkedListStack<string>();
        var b = new StandardStackAdapter<string>();
        foreach (var v in new[] { "a", "b", "c" }) { a.Push(v); b.Push(v); }
        while (!a.IsEmpty()) Assert.Equal(a.Pop(), b.Pop());
    }

    [Fact] public void BothQueues_GiveSameResults()
    {
        var a = new ListQueue<string>();
        var b = new StandardQueueAdapter<string>();
        foreach (var v in new[] { "x", "y", "z" }) { a.Enqueue(v); b.Enqueue(v); }
        while (!a.IsEmpty()) Assert.Equal(a.Dequeue(), b.Dequeue());
    }
}
'@

# Tests/PrefixTreeTests.cs
Write-File "Tests/LabApp.Tests/PrefixTreeTests.cs" @'
using LabApp.Core;
using Xunit;

public class PrefixTreeTests
{
    [Fact] public void ParseSample_BuildsTree()
    {
        var tree = PrefixParser.ParsePrefix("ABD*G***CE**FH**J**");
        Assert.NotNull(tree.Root);
        Assert.Equal('A', tree.Root!.Value);
        Assert.Equal('B', tree.Root.Left!.Value);
        Assert.Equal('D', tree.Root.Left!.Left!.Value);
        Assert.Equal('G', tree.Root.Left!.Left!.Right!.Value);
        Assert.Equal('C', tree.Root.Right!.Value);
        Assert.Equal('E', tree.Root.Right!.Left!.Value);
        Assert.Equal('F', tree.Root.Right!.Right!.Value);
    }

    [Fact] public void Parse_NullTree()
    {
        var tree = PrefixParser.ParsePrefix("**");
        Assert.Null(tree.Root);
    }
}
'@

# ---------- 12. Сборка ----------
Write-Step "Сборка"
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build завершился с ошибкой (код $LASTEXITCODE). Смотри вывод выше."
}

# ---------- 13. Тесты ----------
Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test завершился с ошибкой (код $LASTEXITCODE). Смотри вывод выше."
}

# ---------- 14. Итог ----------
Write-Step "Готово"
Write-Host "  Solution: $sln"
Write-Host "  Папка:    $rootPath"
Write-Host ""
Write-Host "  Проверь:"
Write-Host "    dotnet run --project LabApp"
Write-Host "    open http://localhost:5000"
Write-Host ""
Write-Host "  Результат тестов должен быть: ~24 passed, 0 failed"