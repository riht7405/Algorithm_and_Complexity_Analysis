<#
.SYNOPSIS
    Шаг 7: корректные подписи на графике + страница справки docs.html.
#>
[CmdletBinding()]
param([string]$Root = ".")

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Info([string]$msg) { Write-Host "  $msg" }

Set-Location (Resolve-Path $Root).Path

if (-not (Test-Path "LabApp/wwwroot/app.js")) { throw "Нет app.js. Сначала Шаги 4-6." }
if (-not (Test-Path "LabApp/Core/SinglyLinkedList.cs")) { throw "Нет Core/SinglyLinkedList.cs." }

# ---------- 1. Читаем исходники для страницы справки ----------
Write-Step "Чтение исходников структур"
function Read-Src([string]$path) {
    return [System.IO.File]::ReadAllText((Join-Path (Get-Location) $path), [System.Text.Encoding]::UTF8)
}
$listNodeSrc = Read-Src "LabApp/Core/ListNode.cs"
$listSrc     = Read-Src "LabApp/Core/SinglyLinkedList.cs"
$stackSrc    = Read-Src "LabApp/Core/Stack.cs"
$queueSrc    = Read-Src "LabApp/Core/Queue.cs"
Write-Info "ListNode.cs, SinglyLinkedList.cs, Stack.cs, Queue.cs прочитаны"

function Html([string]$s) { return [System.Net.WebUtility]::HtmlEncode($s) }

$escListNode = Html $listNodeSrc
$escList     = Html $listSrc
$escStack    = Html $stackSrc
$escQueue    = Html $queueSrc

# ---------- 2. Фикс легенды в app.js ----------
Write-Step "app.js: честные подписи графика"
$jsPath = "LabApp/wwwroot/app.js"
$c = [System.IO.File]::ReadAllText($jsPath, [System.Text.Encoding]::UTF8)
$c = $c.Replace('label: "Своя структура (эмпирика)"',      'label: "Своя структура"')
$c = $c.Replace('label: "Стандартная .NET (аппроксимация)"','label: "Стандартная .NET"')
[System.IO.File]::WriteAllText($jsPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "легенда исправлена"

# ---------- 3. index.html: ссылка на справку + обновлённая подпись ----------
Write-Step "index.html: ссылка и подпись"
$htmlPath = "LabApp/wwwroot/index.html"
$c = [System.IO.File]::ReadAllText($htmlPath, [System.Text.Encoding]::UTF8)

# 3.1. Оборачиваем tabs в flex-контейнер с ссылкой «Справка →» справа
$tabsOld = @'
  <nav class="tabs">
    <button class="tab active" data-tab="stack">Stack</button>
    <button class="tab" data-tab="queue">Queue</button>
    <button class="tab" data-tab="expr">Expressions</button>
    <button class="tab" data-tab="lists">Lists</button>
    <button class="tab" data-tab="tree">Tree</button>
    <button class="tab" data-tab="bench">Benchmarks</button>
  </nav>
'@

$tabsNew = @'
  <div class="tabs-row">
    <nav class="tabs">
      <button class="tab active" data-tab="stack">Stack</button>
      <button class="tab" data-tab="queue">Queue</button>
      <button class="tab" data-tab="expr">Expressions</button>
      <button class="tab" data-tab="lists">Lists</button>
      <button class="tab" data-tab="tree">Tree</button>
      <button class="tab" data-tab="bench">Benchmarks</button>
    </nav>
    <a class="docs-link" href="docs.html">Справка по реализациям →</a>
  </div>
'@

$c = $c.Replace($tabsOld, $tabsNew)

# 3.2. Подпись под графиком
$c = $c.Replace(
    '<p class="muted small">Оси логарифмические. Красная — своя структура, синяя — стандартная. Ratio = своя / стандартная.</p>',
    '<p class="muted small">Обе линии — реальные замеры. Точки — своя структура (на связном списке), линия — стандартная .NET. Ratio = своя / стандартная. Оси линейные, шаг по N регулируется полями сверху.</p>'
)

[System.IO.File]::WriteAllText($htmlPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "шапка и подпись обновлены"

# ---------- 4. styles.css: стили для ссылки и страницы справки ----------
Write-Step "styles.css: стили"
$cssPath = "LabApp/wwwroot/styles.css"
$css = [System.IO.File]::ReadAllText($cssPath, [System.Text.Encoding]::UTF8)

$append = @'

/* ---- tabs-row + ссылка на справку ---- */
.tabs-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-top: 1rem;
}
.tabs-row .tabs { margin-top: 0; }
.docs-link {
  color: var(--accent);
  text-decoration: none;
  font-size: 0.95rem;
  padding-bottom: 0.6rem;
  border-bottom: 2px solid transparent;
}
.docs-link:hover { border-bottom-color: var(--accent); }

/* ---- страница справки ---- */
.docs-layout {
  display: grid;
  grid-template-columns: 220px 1fr;
  gap: 2rem;
  max-width: 1200px;
}
@media (max-width: 800px) {
  .docs-layout { grid-template-columns: 1fr; }
  .docs-toc { position: static !important; }
}
.docs-toc {
  position: sticky;
  top: 1.5rem;
  align-self: start;
  padding: 1rem;
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 8px;
}
.docs-toc h3 { margin: 0 0 .5rem; font-size: 1rem; }
.docs-toc ul { margin: 0; padding: 0 0 0 1rem; }
.docs-toc li { margin: .25rem 0; }
.docs-toc a { color: var(--accent); text-decoration: none; }
.docs-toc a:hover { text-decoration: underline; }

.docs-content section { margin-bottom: 3rem; }
.docs-content section h2 {
  border-bottom: 2px solid var(--line);
  padding-bottom: .5rem;
  margin-top: 0;
}
.docs-content h3 {
  margin-top: 1.5rem;
  color: var(--fg);
}
.docs-content p { line-height: 1.6; }

.code-block {
  background: #0f172a;
  color: #e2e8f0;
  padding: 1rem 1.25rem;
  border-radius: 8px;
  overflow-x: auto;
  font-family: "Cascadia Code", "Fira Code", Consolas, monospace;
  font-size: 0.85rem;
  line-height: 1.55;
  margin: 1rem 0;
}
.code-block code { background: none; color: inherit; padding: 0; font-size: inherit; }

.complexity {
  border-collapse: collapse;
  margin: 1rem 0;
  font-size: 0.92rem;
}
.complexity th, .complexity td {
  border: 1px solid var(--line);
  padding: .4rem .8rem;
  text-align: left;
}
.complexity th { background: #f1f5f9; font-weight: 600; }
.complexity td:nth-child(2) { font-family: Consolas, monospace; }
'@

[System.IO.File]::WriteAllText($cssPath, $css + $append, [System.Text.UTF8Encoding]::new($true))
Write-Info "стили добавлены"

# ---------- 5. docs.html ----------
Write-Step "docs.html"

$docsHtml = @"
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>LabApp — справка по структурам</title>
<link rel="stylesheet" href="styles.css">
</head>
<body>
<header>
  <h1>Справка по структурам</h1>
  <p class="muted">Реализации, которые используются в приложении. Код — из <code>LabApp/Core/</code>.</p>
  <div class="tabs-row">
    <nav class="tabs">
      <a class="tab" href="index.html" style="text-decoration:none;">← К приложению</a>
    </nav>
  </div>
</header>

<main>
<div class="docs-layout">
  <aside class="docs-toc">
    <h3>Содержание</h3>
    <ul>
      <li><a href="#list">Связный список</a></li>
      <li><a href="#stack">Стек</a></li>
      <li><a href="#queue">Очередь</a></li>
    </ul>
    <h3 style="margin-top:1.5rem;">Что смотреть</h3>
    <p class="muted small">
      Свои структуры построены на одном примитиве — односвязном списке.
      Стек и очередь — тонкие адаптеры над ним, плюс адаптеры над стандартными
      коллекциями .NET для сравнения в разделе Benchmarks.
    </p>
  </aside>

  <article class="docs-content">

    <!-- ============ СВЯЗНЫЙ СПИСОК ============ -->
    <section id="list">
      <h2>Связный список</h2>
      <p>
        Односвязный список с указателями на первый и последний узел.
        Хранит счётчик элементов, поэтому <code>Count</code> за O(1).
        Указатель на хвост (<code>Tail</code>) даёт O(1) на добавление в конец.
        Удаления из конца O(n) — обратного указателя нет.
      </p>

      <h3>Узел списка</h3>
      <pre class="code-block"><code>$escListNode</code></pre>

      <h3>Класс SinglyLinkedList</h3>
      <pre class="code-block"><code>$escList</code></pre>

      <h3>Сложность операций</h3>
      <table class="complexity">
        <thead><tr><th>Операция</th><th>Сложность</th><th>Комментарий</th></tr></thead>
        <tbody>
          <tr><td>AddFirst</td><td>O(1)</td><td>перекидываем <code>Head</code></td></tr>
          <tr><td>AddLast</td><td>O(1)</td><td>благодаря <code>Tail</code></td></tr>
          <tr><td>RemoveFirst</td><td>O(1)</td><td></td></tr>
          <tr><td>RemoveLast</td><td>O(n)</td><td>нужно дойти до предпоследнего</td></tr>
          <tr><td>Items</td><td>O(n)</td><td>итератор</td></tr>
          <tr><td>Clone</td><td>O(n)</td><td>копия всех узлов</td></tr>
        </tbody>
      </table>

      <p class="muted small">
        Все 12 алгоритмов (reverse, swap, split, ...) живут в
        <code>LinkedListAlgorithms.cs</code> и работают поверх этого класса.
      </p>
    </section>

    <!-- ============ СТЕК ============ -->
    <section id="stack">
      <h2>Стек</h2>
      <p>
        Интерфейс <code>IStack&lt;T&gt;</code> — контракт для обеих реализаций.
        <code>LinkedListStack&lt;T&gt;</code> использует наш односвязный список:
        <code>Push</code> = <code>AddFirst</code>, <code>Pop</code> = <code>RemoveFirst</code>.
        <code>StandardStackAdapter&lt;T&gt;</code> оборачивает <code>System.Collections.Generic.Stack&lt;T&gt;</code>
        и нужен, чтобы в разделе Benchmarks обе реализации шли через один и тот же интерфейс.
      </p>

      <h3>Интерфейс и реализации</h3>
      <pre class="code-block"><code>$escStack</code></pre>

      <h3>Сложность операций</h3>
      <table class="complexity">
        <thead><tr><th>Операция</th><th>Сложность</th></tr></thead>
        <tbody>
          <tr><td>Push</td><td>O(1)</td></tr>
          <tr><td>Pop</td><td>O(1)</td></tr>
          <tr><td>Top</td><td>O(1)</td></tr>
          <tr><td>IsEmpty</td><td>O(1)</td></tr>
          <tr><td>Print</td><td>O(n)</td></tr>
        </tbody>
      </table>

      <p class="muted small">
        Почему обе реализации идут через <code>IStack&lt;T&gt;</code> в бенчмарках:
        так интерфейсная диспетчеризация одинакова, и разница во времени отражает
        именно разницу структур, а не разницу API. См. ADR-0005.
      </p>
    </section>

    <!-- ============ ОЧЕРЕДЬ ============ -->
    <section id="queue">
      <h2>Очередь</h2>
      <p>
        Интерфейс <code>IQueue&lt;T&gt;</code>, две реализации по той же схеме.
        <code>ListQueue&lt;T&gt;</code> использует наш список:
        <code>Enqueue</code> = <code>AddLast</code>, <code>Dequeue</code> = <code>RemoveFirst</code>.
        Оба O(1), потому что список хранит и <code>Head</code>, и <code>Tail</code>.
        <code>StandardQueueAdapter&lt;T&gt;</code> оборачивает
        <code>System.Collections.Generic.Queue&lt;T&gt;</code> (ring buffer внутри).
      </p>

      <h3>Интерфейс и реализации</h3>
      <pre class="code-block"><code>$escQueue</code></pre>

      <h3>Сложность операций</h3>
      <table class="complexity">
        <thead><tr><th>Операция</th><th>Сложность</th></tr></thead>
        <tbody>
          <tr><td>Enqueue</td><td>O(1)</td></tr>
          <tr><td>Dequeue</td><td>O(1)</td></tr>
          <tr><td>Peek</td><td>O(1)</td></tr>
          <tr><td>IsEmpty</td><td>O(1)</td></tr>
          <tr><td>Print</td><td>O(n)</td></tr>
        </tbody>
      </table>

      <p class="muted small">
        Разница со стандартной очередью на графике Benchmarks объясняется
        cache locality (у стандартной — непрерывный массив) и аллокацией узла
        на каждый элемент у нас.
      </p>
    </section>

  </article>
</div>
</main>
</body>
</html>
"@

[System.IO.File]::WriteAllText(
    (Join-Path (Get-Location) "LabApp/wwwroot/docs.html"),
    $docsHtml,
    [System.Text.UTF8Encoding]::new($true)
)
Write-Info "+ LabApp/wwwroot/docs.html"

# ---------- 6. Сборка и тесты ----------
Write-Step "Сборка"
$sln = if (Test-Path "LabApp.slnx") { "LabApp.slnx" } else { "LabApp.sln" }
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build упал" }

Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet test упал" }

Write-Step "Готово"
Write-Host "  Запускай: dotnet run --project LabApp"
Write-Host "  Открой http://localhost:5000"
Write-Host ""
Write-Host "  Что изменилось:"
Write-Host "    - Benchmarks: подписи 'Своя структура' / 'Стандартная .NET' (без эмпирики/аппроксимации)"
Write-Host "    - В шапке — ссылка 'Справка по реализациям ->'"
Write-Host "    - Отдельная страница docs.html: связный список, стек, очередь"
Write-Host "    - Для каждой структуры: описание, код из Core/, таблица сложности"