<#
.SYNOPSIS
    Шаг 10: страница apply.html с интерактивными применениями структур.
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
if (-not (Test-Path "LabApp/wwwroot/index.html")) { throw "Нет index.html." }

# ---------- 1. index.html: ссылка "Применения →" рядом с "Справка →" ----------
Write-Step "index.html: ссылка на применения"
$htmlPath = "LabApp/wwwroot/index.html"
$c = [System.IO.File]::ReadAllText($htmlPath, [System.Text.Encoding]::UTF8)
$old = '<a class="docs-link" href="docs.html">Справка по реализациям →</a>'
$new = @'
<div style="display:flex; gap:1.25rem; padding-bottom:0.6rem;">
      <a class="docs-link" href="apply.html">Применения →</a>
      <a class="docs-link" href="docs.html">Справка →</a>
    </div>
'@
$c = $c.Replace($old, $new.Trim())
[System.IO.File]::WriteAllText($htmlPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "ссылка добавлена"

# ---------- 2. apply.html ----------
Write-Step "apply.html"

Write-File "LabApp/wwwroot/apply.html" @'
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>LabApp — применения структур</title>
<link rel="stylesheet" href="styles.css">
</head>
<body>
<header>
  <h1>Применения структур данных</h1>
  <p class="muted">Реальные задачи из индустрии с интерактивными демонстрациями</p>
  <div class="tabs-row">
    <nav class="tabs">
      <a class="tab" href="index.html" style="text-decoration:none;">← К приложению</a>
    </nav>
    <div style="display:flex; gap:1.25rem; padding-bottom:0.6rem;">
      <a class="docs-link" href="docs.html">Справка по реализациям →</a>
    </div>
  </div>
</header>

<main>
<nav class="apply-nav">
  <a href="#stack">Стек</a>
  <a href="#list">Список</a>
  <a href="#queue">Очередь</a>
  <a href="#tree">Дерево</a>
</nav>

<!-- ============ STACK ============ -->
<section id="stack" class="apply-section">
  <h2>Стек: Killer Heuristic в шахматных движках</h2>
  <div class="apply-grid">
    <div class="apply-text">
      <h3>Условие</h3>
      <p>Шахматный движок перебирает миллионы позиций. Нужно быстро находить ходы, которые с наибольшей вероятностью приведут к отсечению (beta-cutoff), чтобы не перебирать заведомо бесполезные ветки и сэкономить время.</p>
      <h3>Логика</h3>
      <p>Применяется <i>Killer Heuristic</i>. Используется стек, где каждый уровень — это глубина поиска (ply). В каждом узле стека хранятся 1–2 «тихих» хода, которые ранее уже приводили к отсечению на этой же глубине. Поиск рекурсивный: заходим вглубь — кладём новый уровень, выходим — снимаем. Стек естественно ложится на эту структуру вызовов.</p>
      <h3>Где используется</h3>
      <p>Stockfish, Crafty, Houdini, Komodo. В современных движках killer heuristic частично вытеснен NNUE и history heuristic, но остаётся каноническим примером «стек как состояние алгоритма».</p>
      <h3>Сложность</h3>
      <p>Push/pop на стек — <code>O(1)</code>. Обновление killers — <code>O(1)</code> на отсечение. Экономия от pruning — до 50% узлов на средней позиции.</p>
    </div>
    <div class="apply-demo">
      <h3>Демо: стек killers</h3>
      <p class="muted small">Каждый уровень — глубина поиска. В двух ячейках хранятся ходы-убийцы.</p>
      <div id="stack-visual" class="stack-visual"></div>
      <div class="demo-controls">
        <button id="stack-cutoff">Симулировать отсечение</button>
        <button id="stack-reset" class="secondary">Сброс</button>
      </div>
      <div id="stack-log" class="demo-log">Нажми «Симулировать отсечение»</div>
    </div>
  </div>
</section>

<!-- ============ LIST ============ -->
<section id="list" class="apply-section">
  <h2>Список: Dancing Links для Судоку 4×4</h2>
  <div class="apply-grid">
    <div class="apply-text">
      <h3>Условие</h3>
      <p>Дана задача точного покрытия (Exact Cover) — Судоку 4×4. Нужно перебирать варианты с откатом за минимальное время, чтобы не тратить часы на перебор.</p>
      <h3>Логика</h3>
      <p>Алгоритм X Дональда Кнута с представлением матрицы ограничений через <i>Dancing Links</i>. Каждый узел — двусвязный по горизонтали и вертикали. Когда алгоритм делает ход, он «вырезает» выбранный узел и все конфликтующие с ним. Тупик — узлы «вставляются» обратно (откат). Откат за <code>O(1)</code> — вот в чём сила структуры.</p>
      <h3>Где используется</h3>
      <p>Судоку, задача N ферзей, замощение полимино, точное покрытие в SAT-солверах. Сам Кнут называл это «танцующими ссылками» за визуальную элегантность.</p>
      <h3>Сложность</h3>
      <p>Обход: <code>O(rows × cols)</code>. Откат: <code>O(1)</code> за счёт сохранённых ссылок. Память: <code>O(n²)</code> узлов для матрицы n×n.</p>
    </div>
    <div class="apply-demo">
      <h3>Демо: Судоку 4×4</h3>
      <p class="muted small">Жёлтые — исходные числа. Зелёные — поставленные. Красные — пробные.</p>
      <div id="sudoku-grid" class="sudoku-grid"></div>
      <div class="demo-controls">
        <button id="sudoku-step">Один шаг</button>
        <button id="sudoku-solve">Решить всё</button>
        <button id="sudoku-reset" class="secondary">Сброс</button>
      </div>
      <div class="demo-stats">
        <span>Итераций: <b id="sudoku-iter">0</b></span>
        <span>Откатов: <b id="sudoku-back">0</b></span>
      </div>
      <div id="sudoku-log" class="demo-log">Нажми «Один шаг»</div>
    </div>
  </div>
</section>

<!-- ============ QUEUE ============ -->
<section id="queue" class="apply-section">
  <h2>Очередь: игра «Змейка»</h2>
  <div class="apply-grid">
    <div class="apply-text">
      <h3>Условие</h3>
      <p>Управлять змейкой на поле. Она движется, ест яблоки, растёт и не должна врезаться в свой хвост. Каждый шаг — сдвиг всей змейки на одну клетку.</p>
      <h3>Логика</h3>
      <p>Тело змейки — это очередь. В начале — хвост, в конце — голова. При каждом шаге мы добавляем новую координату головы в конец (<code>enqueue</code>), а старую координату хвоста удаляем из начала (<code>dequeue</code>). Съели яблоко — добавляем голову, но <b>не удаляем</b> хвост: змейка растёт. Очередь гарантирует, что все сегменты движутся друг за другом без «разрывов».</p>
      <h3>Где используется</h3>
      <p>Классические игры «Змейка», «Трон», «Slither.io». Очередь здесь — не хранилище данных, а <b>модель движения</b> объекта.</p>
      <h3>Сложность</h3>
      <p>Enqueue/dequeue — <code>O(1)</code>. Проверка столкновений — <code>O(k)</code>, где k — длина змейки (можно ускорить до <code>O(1)</code> через HashSet позиций).</p>
    </div>
    <div class="apply-demo">
      <h3>Демо: Змейка</h3>
      <p class="muted small">Управление: стрелки или WASD. Пробел — пауза.</p>
      <div class="snake-wrap">
        <canvas id="snake-canvas" width="360" height="360"></canvas>
        <div class="snake-side">
          <div class="demo-stats" style="flex-direction:column; align-items:flex-start;">
            <span>Счёт: <b id="snake-score">0</b></span>
            <span>Длина очереди: <b id="snake-len">3</b></span>
            <span>Голова: <b id="snake-head">—</b></span>
          </div>
          <div id="snake-queue" class="queue-view"></div>
        </div>
      </div>
      <div class="demo-controls">
        <button id="snake-start">Старт</button>
        <button id="snake-pause" class="secondary">Пауза</button>
      </div>
      <div id="snake-log" class="demo-log">Нажми «Старт» и используй стрелки</div>
    </div>
  </div>
</section>

<!-- ============ TREE ============ -->
<section id="tree" class="apply-section">
  <h2>Дерево: Behavior Tree для игрового AI</h2>
  <div class="apply-grid">
    <div class="apply-text">
      <h3>Условие</h3>
      <p>Описать сложное поведение NPC (патрулировать, преследовать, атаковать). Код не должен превращаться в «лапшу» из if-else с десятком флагов.</p>
      <h3>Логика</h3>
      <p>Используется <i>Behavior Tree</i> — иерархическое дерево исполнения. Листья — действия и условия, внутренние узлы — операторы: <b>Selector</b> (пробует детей пока один не успешен) и <b>Sequence</b> (выполняет по порядку пока все успешны). На каждом тике дерево обходится сверху вниз, выбирая актуальную ветку. Активная ветка подсвечивается — видно, почему NPC делает именно это.</p>
      <h3>Где используется</h3>
      <p>Unreal Engine (встроенная система Behavior Tree), Halo, BioShock, F.E.A.R. Стандарт де-факто для игрового AI. Комбинируется с Blackboard — общей памятью дерева.</p>
      <h3>Сложность</h3>
      <p>Обход за тик: <code>O(n)</code>, где n — число узлов в дереве. На практике n ≈ 10–50, поэтому стоимость незаметна.</p>
    </div>
    <div class="apply-demo">
      <h3>Демо: NPC на дереве поведения</h3>
      <p class="muted small">Синий — NPC, оранжевый — игрок. Нажми «Шаг» — NPC примет решение по дереву.</p>
      <div class="bt-wrap">
        <div class="bt-tree">
          <svg id="bt-svg" width="340" height="320"></svg>
        </div>
        <div class="bt-world">
          <canvas id="bt-canvas" width="220" height="220"></canvas>
        </div>
      </div>
      <div class="demo-controls">
        <button id="bt-step">Шаг</button>
        <button id="bt-auto" class="secondary">Авто</button>
        <button id="bt-reset" class="secondary">Сброс</button>
      </div>
      <div id="bt-log" class="demo-log">Нажми «Шаг»</div>
    </div>
  </div>
</section>

</main>
<script src="apply.js"></script>
</body>
</html>
'@

# ---------- 3. styles.css: стили ----------
Write-Step "styles.css: стили apply"
$cssPath = "LabApp/wwwroot/styles.css"
$css = [System.IO.File]::ReadAllText($cssPath, [System.Text.Encoding]::UTF8)
$append = @'

/* ---- Apply page ---- */
.apply-nav {
  display: flex;
  gap: 1rem;
  padding: 1rem 0;
  margin-bottom: 1rem;
  border-bottom: 1px solid var(--line);
  position: sticky;
  top: 0;
  background: var(--bg);
  z-index: 10;
}
.apply-nav a {
  color: var(--accent);
  text-decoration: none;
  font-weight: 500;
}
.apply-nav a:hover { text-decoration: underline; }

.apply-section { margin-bottom: 3rem; scroll-margin-top: 4rem; }
.apply-section h2 {
  border-bottom: 2px solid var(--line);
  padding-bottom: .5rem;
}

.apply-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 2rem;
  margin-top: 1rem;
}
@media (max-width: 900px) {
  .apply-grid { grid-template-columns: 1fr; }
}

.apply-text h3 {
  margin: 1.25rem 0 .35rem;
  color: var(--accent);
  font-size: 1rem;
  text-transform: uppercase;
  letter-spacing: .04em;
}
.apply-text h3:first-child { margin-top: 0; }
.apply-text p { line-height: 1.6; margin: 0 0 .5rem; color: #374151; }

.apply-demo {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 8px;
  padding: 1.25rem;
}
.apply-demo h3 { margin-top: 0; }

.demo-controls { display: flex; gap: .5rem; flex-wrap: wrap; margin: 1rem 0; }
.demo-controls button.secondary {
  background: #e5e7eb;
  color: var(--fg);
}
.demo-controls button.secondary:hover { background: #d1d5db; }

.demo-log {
  margin-top: .75rem;
  padding: .6rem .8rem;
  background: #f3f4f6;
  border-radius: 6px;
  font-size: .9rem;
  color: var(--muted);
  min-height: 2rem;
}
.demo-stats {
  display: flex;
  gap: 1.5rem;
  margin-top: .75rem;
  font-size: .9rem;
  color: var(--muted);
}
.demo-stats b { color: var(--fg); }

/* ---- Stack visual ---- */
.stack-visual {
  display: flex;
  flex-direction: column-reverse;
  gap: .35rem;
  min-height: 320px;
  padding: .5rem;
  background: #f9fafb;
  border-radius: 6px;
}
.stack-level {
  display: flex;
  align-items: center;
  gap: .5rem;
  padding: .5rem .75rem;
  background: white;
  border: 1px solid var(--line);
  border-radius: 6px;
  transition: all .3s ease;
}
.stack-level.active {
  border-color: var(--accent);
  box-shadow: 0 0 0 2px rgba(11, 107, 203, 0.15);
  transform: translateX(4px);
}
.level-label {
  font-family: Consolas, monospace;
  font-size: .85rem;
  color: var(--muted);
  min-width: 50px;
}
.killer {
  flex: 1;
  padding: .3rem .5rem;
  border-radius: 4px;
  text-align: center;
  font-family: Consolas, monospace;
  font-size: .85rem;
  border: 1px dashed #cbd5e1;
  color: #94a3b8;
}
.killer.filled {
  border-style: solid;
  border-color: #3b82f6;
  background: #eff6ff;
  color: #1e40af;
  font-weight: 600;
}
@keyframes pop {
  0% { transform: scale(1); }
  50% { transform: scale(1.08); }
  100% { transform: scale(1); }
}
.killer.fresh { animation: pop .4s ease; }

/* ---- Sudoku ---- */
.sudoku-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 2px;
  background: #1f2937;
  padding: 3px;
  border-radius: 6px;
  width: 240px;
  margin: 0 auto;
}
.sudoku-cell {
  aspect-ratio: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  background: white;
  font-size: 1.5rem;
  font-weight: 600;
  font-family: Consolas, monospace;
  transition: background .2s, color .2s;
}
.sudoku-cell.given { background: #fef3c7; color: #92400e; }
.sudoku-cell.placed { background: #d1fae5; color: #065f46; }
.sudoku-cell.trying { background: #fee2e2; color: #991b1b; }
.sudoku-cell.conflict { background: #fca5a5; color: white; }
/* borders for 2x2 boxes */
.sudoku-cell:nth-child(2n) { border-right: 2px solid #1f2937; }
.sudoku-cell:nth-child(n+1):nth-child(-n+4),
.sudoku-cell:nth-child(n+9):nth-child(-n+12) {
  border-bottom: 2px solid #1f2937;
}
.sudoku-grid .sudoku-cell:nth-child(4) { border-right: 3px solid #1f2937; }
.sudoku-grid .sudoku-cell:nth-child(8) { border-right: 3px solid #1f2937; }

/* ---- Snake ---- */
.snake-wrap {
  display: flex;
  gap: 1rem;
  align-items: flex-start;
}
#snake-canvas {
  border: 2px solid #1f2937;
  border-radius: 6px;
  background: #0f172a;
}
.snake-side {
  flex: 1;
  min-width: 0;
}
.queue-view {
  margin-top: .75rem;
  padding: .5rem;
  background: #f9fafb;
  border-radius: 6px;
  font-family: Consolas, monospace;
  font-size: .8rem;
  max-height: 220px;
  overflow-y: auto;
}
.queue-view .qitem {
  padding: .2rem .4rem;
  border-radius: 3px;
  margin-bottom: 2px;
}
.queue-view .qitem.head { background: #dbeafe; color: #1e40af; font-weight: 600; }
.queue-view .qitem.tail { background: #fef3c7; color: #92400e; }

/* ---- Behavior Tree ---- */
.bt-wrap {
  display: flex;
  gap: 1rem;
  align-items: flex-start;
  justify-content: center;
  flex-wrap: wrap;
}
.bt-tree svg { display: block; }
.bt-world canvas {
  border: 2px solid #1f2937;
  border-radius: 6px;
  background: #f1f5f9;
}
.bt-node circle { transition: all .25s ease; }
.bt-node text { pointer-events: none; user-select: none; font-family: Consolas, monospace; }
'@
[System.IO.File]::WriteAllText($cssPath, $css + $append, [System.Text.UTF8Encoding]::new($true))
Write-Info "стили добавлены"

# ---------- 4. apply.js ----------
Write-Step "apply.js"

Write-File "LabApp/wwwroot/apply.js" @'
// ============================================================
// 1. STACK: Killer Heuristic
// ============================================================
(function () {
  const LEVELS = 6;
  const MOVES = ["e2e4", "g1f3", "d2d4", "b1c3", "f1c4", "e7e5", "b8c6", "d2d5", "c2c4", "a2a3"];
  let killers = Array.from({ length: LEVELS }, () => [null, null]);
  let fresh = null;

  function render() {
    const el = document.getElementById("stack-visual");
    if (!el) return;
    let html = "";
    for (let i = 0; i < LEVELS; i++) {
      const [k0, k1] = killers[i];
      html += '<div class="stack-level"><span class="level-label">ply ' + i + '</span>' +
        '<span class="killer ' + (k0 ? 'filled' : '') + '">' + (k0 || '—') + '</span>' +
        '<span class="killer ' + (k1 ? 'filled' : '') + '">' + (k1 || '—') + '</span>' +
        '</div>';
    }
    el.innerHTML = html;
    if (fresh !== null) {
      const levels = el.querySelectorAll(".stack-level");
      if (levels[fresh]) {
        const killersEls = levels[fresh].querySelectorAll(".killer");
        if (killersEls[0]) killersEls[0].classList.add("fresh");
      }
      fresh = null;
    }
  }

  document.getElementById("stack-cutoff").addEventListener("click", () => {
    const level = Math.floor(Math.random() * LEVELS);
    const move = MOVES[Math.floor(Math.random() * MOVES.length)];
    killers[level][1] = killers[level][0];
    killers[level][0] = move;
    fresh = level;
    render();
    document.getElementById("stack-log").textContent =
      "Beta-cutoff на глубине " + level + " ходом " + move +
      " → killers[" + level + "] = [" + killers[level].join(", ") + "]";
  });

  document.getElementById("stack-reset").addEventListener("click", () => {
    killers = Array.from({ length: LEVELS }, () => [null, null]);
    render();
    document.getElementById("stack-log").textContent = "Сброшено";
  });

  render();
})();

// ============================================================
// 2. LIST: Sudoku 4x4 (Dancing Links)
// ============================================================
(function () {
  // . = empty
  const PUZZLE = [
    [0, 1, 0, 0],
    [0, 0, 0, 2],
    [0, 0, 4, 0],
    [0, 0, 0, 0]
  ];
  let board = JSON.parse(JSON.stringify(PUZZLE));
  let original = JSON.parse(JSON.stringify(PUZZLE));
  let steps = [];
  let stepIdx = 0;
  let iterations = 0;
  let backtracks = 0;

  function isValid(b, r, c, v) {
    for (let i = 0; i < 4; i++) if (b[r][i] === v || b[i][c] === v) return false;
    const br = Math.floor(r / 2) * 2, bc = Math.floor(c / 2) * 2;
    for (let i = 0; i < 2; i++) for (let j = 0; j < 2; j++)
      if (b[br + i][bc + j] === v) return false;
    return true;
  }

  function solve(b, r, c, path) {
    if (r === 4) return true;
    if (c === 4) return solve(b, r + 1, 0, path);
    if (b[r][c] !== 0) return solve(b, r, c + 1, path);
    for (let v = 1; v <= 4; v++) {
      if (!isValid(b, r, c, v)) {
        path.push({ type: "conflict", r, c, v });
        continue;
      }
      b[r][c] = v;
      path.push({ type: "place", r, c, v });
      if (solve(b, r, c + 1, path)) return true;
      b[r][c] = 0;
      path.push({ type: "backtrack", r, c, v });
    }
    return false;
  }

  function computeAll() {
    const b = JSON.parse(JSON.stringify(original));
    const path = [];
    solve(b, 0, 0, path);
    return path;
  }

  function renderCell(r, c) {
    const v = board[r][c];
    const given = original[r][c] !== 0;
    let cls = "sudoku-cell";
    if (given) cls += " given";
    else if (v !== 0) cls += " placed";
    return '<div class="' + cls + '" id="cell-' + r + '-' + c + '">' + (v || '') + '</div>';
  }

  function renderGrid() {
    let html = "";
    for (let r = 0; r < 4; r++)
      for (let c = 0; c < 4; c++)
        html += renderCell(r, c);
    document.getElementById("sudoku-grid").innerHTML = html;
  }

  function renderStats() {
    document.getElementById("sudoku-iter").textContent = iterations;
    document.getElementById("sudoku-back").textContent = backtracks;
  }

  function reset() {
    board = JSON.parse(JSON.stringify(PUZZLE));
    original = JSON.parse(JSON.stringify(PUZZLE));
    steps = computeAll();
    stepIdx = 0;
    iterations = 0;
    backtracks = 0;
    renderGrid();
    renderStats();
    document.getElementById("sudoku-log").textContent = "Нажми «Один шаг» или «Решить всё»";
  }

  function applyStep(step) {
    iterations++;
    const cell = document.getElementById("cell-" + step.r + "-" + step.c);
    if (step.type === "place") {
      board[step.r][step.c] = step.v;
      if (cell) { cell.textContent = step.v; cell.className = "sudoku-cell trying"; }
      document.getElementById("sudoku-log").textContent =
        "Попытка поставить " + step.v + " в (" + step.r + ", " + step.c + ")";
    } else if (step.type === "backtrack") {
      board[step.r][step.c] = 0;
      backtracks++;
      if (cell) { cell.textContent = ""; cell.className = "sudoku-cell trying"; }
      document.getElementById("sudoku-log").textContent =
        "Откат: не подошло " + step.v + " в (" + step.r + ", " + step.c + ")";
    } else if (step.type === "conflict") {
      if (cell) { cell.classList.add("conflict"); }
      document.getElementById("sudoku-log").textContent =
        "Конфликт: " + step.v + " в (" + step.r + ", " + step.c + ") не подходит";
    }
  }

  function cleanupTrying() {
    document.querySelectorAll(".sudoku-cell.trying").forEach(e => {
      e.classList.remove("trying");
      if (e.textContent) e.classList.add("placed");
    });
    document.querySelectorAll(".sudoku-cell.conflict").forEach(e => e.classList.remove("conflict"));
  }

  document.getElementById("sudoku-step").addEventListener("click", () => {
    if (stepIdx >= steps.length) {
      document.getElementById("sudoku-log").textContent = "Всё, решено!";
      return;
    }
    cleanupTrying();
    applyStep(steps[stepIdx++]);
    renderStats();
  });

  document.getElementById("sudoku-solve").addEventListener("click", () => {
    const tick = () => {
      if (stepIdx >= steps.length) {
        cleanupTrying();
        document.getElementById("sudoku-log").textContent =
          "Решено за " + iterations + " итераций, " + backtracks + " откатов.";
        return;
      }
      cleanupTrying();
      applyStep(steps[stepIdx++]);
      renderStats();
      setTimeout(tick, 80);
    };
    tick();
  });

  document.getElementById("sudoku-reset").addEventListener("click", reset);

  reset();
})();

// ============================================================
// 3. QUEUE: Snake
// ============================================================
(function () {
  const COLS = 15, ROWS = 15, CELL = 24;
  const canvas = document.getElementById("snake-canvas");
  if (!canvas) return;
  const ctx = canvas.getContext("2d");
  canvas.width = COLS * CELL;
  canvas.height = ROWS * CELL;

  let queue = [];
  let dir = { x: 1, y: 0 };
  let nextDir = { x: 1, y: 0 };
  let food = { x: 8, y: 8 };
  let score = 0;
  let running = false;
  let paused = false;
  let ticker = null;
  let gameOver = false;

  function reset() {
    queue = [{ x: 5, y: 7 }, { x: 4, y: 7 }, { x: 3, y: 7 }];
    dir = { x: 1, y: 0 }; nextDir = { x: 1, y: 0 };
    score = 0;
    gameOver = false;
    running = false;
    paused = false;
    if (ticker) { clearInterval(ticker); ticker = null; }
    placeFood();
    draw();
    updateSide();
    document.getElementById("snake-log").textContent = "Нажми «Старт»";
  }

  function placeFood() {
    for (let tries = 0; tries < 200; tries++) {
      const f = { x: Math.floor(Math.random() * COLS), y: Math.floor(Math.random() * ROWS) };
      if (!queue.some(s => s.x === f.x && s.y === f.y)) { food = f; return; }
    }
  }

  function step() {
    if (!running || paused) return;
    dir = nextDir;
    const head = queue[queue.length - 1];
    const nh = { x: head.x + dir.x, y: head.y + dir.y };
    if (nh.x < 0 || nh.x >= COLS || nh.y < 0 || nh.y >= ROWS) { end(); return; }
    if (queue.some(s => s.x === nh.x && s.y === nh.y)) { end(); return; }

    queue.push(nh); // enqueue
    if (nh.x === food.x && nh.y === food.y) {
      score++;
      placeFood();
    } else {
      queue.shift(); // dequeue
    }
    draw(); updateSide();
  }

  function end() {
    running = false;
    gameOver = true;
    if (ticker) { clearInterval(ticker); ticker = null; }
    document.getElementById("snake-log").textContent = "Игра окончена. Счёт: " + score;
    draw();
  }

  function draw() {
    ctx.fillStyle = "#0f172a";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    // grid
    ctx.strokeStyle = "rgba(148, 163, 184, 0.15)";
    for (let i = 0; i <= COLS; i++) {
      ctx.beginPath(); ctx.moveTo(i * CELL, 0); ctx.lineTo(i * CELL, canvas.height); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(0, i * CELL); ctx.lineTo(canvas.width, i * CELL); ctx.stroke();
    }
    // food
    ctx.fillStyle = "#ef4444";
    ctx.beginPath();
    ctx.arc(food.x * CELL + CELL / 2, food.y * CELL + CELL / 2, CELL / 2 - 3, 0, Math.PI * 2);
    ctx.fill();
    // snake
    for (let i = 0; i < queue.length; i++) {
      const s = queue[i];
      const isHead = i === queue.length - 1;
      ctx.fillStyle = isHead ? "#22c55e" : "#4ade80";
      ctx.fillRect(s.x * CELL + 2, s.y * CELL + 2, CELL - 4, CELL - 4);
    }
    if (gameOver) {
      ctx.fillStyle = "rgba(0,0,0,0.6)";
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = "white";
      ctx.font = "bold 24px system-ui";
      ctx.textAlign = "center";
      ctx.fillText("Game Over", canvas.width / 2, canvas.height / 2);
    }
  }

  function updateSide() {
    document.getElementById("snake-score").textContent = score;
    document.getElementById("snake-len").textContent = queue.length;
    const head = queue[queue.length - 1];
    if (head) document.getElementById("snake-head").textContent = "(" + head.x + ", " + head.y + ")";
    let html = "";
    for (let i = queue.length - 1; i >= 0; i--) {
      const s = queue[i];
      const cls = i === queue.length - 1 ? "head" : (i === 0 ? "tail" : "");
      const label = i === queue.length - 1 ? "HEAD" : (i === 0 ? "TAIL" : "    ");
      html += '<div class="qitem ' + cls + '">' + label + ' → (' + s.x + ', ' + s.y + ')</div>';
    }
    document.getElementById("snake-queue").innerHTML = html;
  }

  document.addEventListener("keydown", e => {
    const k = e.key.toLowerCase();
    const map = {
      arrowup: { x: 0, y: -1 }, w: { x: 0, y: -1 },
      arrowdown: { x: 0, y: 1 }, s: { x: 0, y: 1 },
      arrowleft: { x: -1, y: 0 }, a: { x: -1, y: 0 },
      arrowright: { x: 1, y: 0 }, d: { x: 1, y: 0 }
    };
    if (map[k]) {
      const nd = map[k];
      if (nd.x !== -dir.x || nd.y !== -dir.y) nextDir = nd;
      e.preventDefault();
    }
    if (k === " ") {
      if (running) paused = !paused;
      e.preventDefault();
    }
  });

  document.getElementById("snake-start").addEventListener("click", () => {
    if (gameOver || queue.length === 0) reset();
    if (!running) {
      running = true;
      paused = false;
      if (ticker) clearInterval(ticker);
      ticker = setInterval(step, 120);
      document.getElementById("snake-log").textContent = "Играем! Стрелки для управления.";
    }
  });

  document.getElementById("snake-pause").addEventListener("click", () => {
    if (running) paused = !paused;
    document.getElementById("snake-log").textContent = paused ? "Пауза" : "Продолжаем";
  });

  reset();
})();

// ============================================================
// 4. TREE: Behavior Tree for NPC
// ============================================================
(function () {
  const svg = document.getElementById("bt-svg");
  const canvas = document.getElementById("bt-canvas");
  if (!svg || !canvas) return;
  const ctx = canvas.getContext("2d");

  const W = 10, H = 10, CELL = 22;
  canvas.width = W * CELL; canvas.height = H * CELL;

  // Tree structure
  const tree = {
    name: "Selector", type: "selector", children: [
      {
        name: "Attack", type: "sequence", children: [
          { name: "InRange?", type: "condition" },
          { name: "Attack!", type: "action" }
        ]
      },
      {
        name: "Chase", type: "sequence", children: [
          { name: "CanSee?", type: "condition" },
          { name: "MoveToPlayer", type: "action" }
        ]
      },
      { name: "Patrol", type: "action" }
    ]
  };

  // Assign layout (simple hierarchical)
  const layout = [
    { node: tree, x: 170, y: 30 },
    { node: tree.children[0], x: 80, y: 100 },
    { node: tree.children[1], x: 170, y: 100 },
    { node: tree.children[2], x: 260, y: 100 },
    { node: tree.children[0].children[0], x: 30, y: 190 },
    { node: tree.children[0].children[1], x: 130, y: 190 },
    { node: tree.children[1].children[0], x: 200, y: 190 },
    { node: tree.children[1].children[1], x: 300, y: 190 }
  ];

  const edges = [
    [0, 1], [0, 2], [0, 3],
    [1, 4], [1, 5], [2, 6], [2, 7]
  ];

  let npc = { x: 2, y: 2 };
  let player = { x: 8, y: 7 };
  let activePath = new Set();
  let activeLeaf = null;
  let autoTimer = null;

  function drawTree() {
    let html = "";
    edges.forEach(([a, b]) => {
      const A = layout[a], B = layout[b];
      const active = activePath.has(a) && activePath.has(b);
      html += '<line x1="' + A.x + '" y1="' + (A.y + 15) + '" x2="' + B.x + '" y2="' + (B.y - 15) +
              '" stroke="' + (active ? "#0b6bcb" : "#cbd5e1") + '" stroke-width="' + (active ? 3 : 1.5) + '"/>';
    });
    layout.forEach((item, i) => {
      const node = item.node;
      const active = activePath.has(i);
      const color = node.type === "selector" ? "#8b5cf6"
                  : node.type === "sequence" ? "#f59e0b"
                  : node.type === "condition" ? "#10b981"
                  : "#3b82f6";
      const fill = active ? color : "#e5e7eb";
      const textColor = active ? "white" : "#6b7280";
      html += '<g class="bt-node">';
      html += '<rect x="' + (item.x - 48) + '" y="' + (item.y - 15) + '" width="96" height="30" rx="6" fill="' + fill + '" stroke="' + color + '" stroke-width="2"/>';
      const label = node.name.length > 14 ? node.name.slice(0, 13) + "…" : node.name;
      html += '<text x="' + item.x + '" y="' + (item.y + 5) + '" text-anchor="middle" fill="' + textColor + '" font-size="11" font-weight="600">' + label + '</text>';
      html += '</g>';
    });
    svg.innerHTML = html;
  }

  function drawWorld() {
    ctx.fillStyle = "#f1f5f9";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.strokeStyle = "#e2e8f0";
    for (let i = 0; i <= W; i++) {
      ctx.beginPath(); ctx.moveTo(i * CELL, 0); ctx.lineTo(i * CELL, canvas.height); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(0, i * CELL); ctx.lineTo(canvas.width, i * CELL); ctx.stroke();
    }
    // player (orange)
    ctx.fillStyle = "#f97316";
    ctx.beginPath();
    ctx.arc(player.x * CELL + CELL / 2, player.y * CELL + CELL / 2, CELL / 2 - 2, 0, Math.PI * 2);
    ctx.fill();
    // NPC (blue)
    ctx.fillStyle = "#0b6bcb";
    ctx.beginPath();
    ctx.arc(npc.x * CELL + CELL / 2, npc.y * CELL + CELL / 2, CELL / 2 - 2, 0, Math.PI * 2);
    ctx.fill();
    // line of sight
    if (activePath.size > 0 && canSee()) {
      ctx.strokeStyle = "rgba(11, 107, 203, 0.3)";
      ctx.setLineDash([4, 4]);
      ctx.beginPath();
      ctx.moveTo(npc.x * CELL + CELL / 2, npc.y * CELL + CELL / 2);
      ctx.lineTo(player.x * CELL + CELL / 2, player.y * CELL + CELL / 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  function dist() {
    const dx = player.x - npc.x, dy = player.y - npc.y;
    return Math.sqrt(dx * dx + dy * dy);
  }
  function inRange() { return dist() <= 1.5; }
  function canSee() { return dist() <= 4; }

  function tick() {
    activePath = new Set([0]);
    activeLeaf = null;

    // Try Attack
    if (inRange()) {
      activePath.add(1); activePath.add(4); activePath.add(5);
      activeLeaf = "Attack!";
      document.getElementById("bt-log").textContent = "NPC: игрок в радиусе → Attack!";
    } else {
      // Try Chase
      activePath.add(1); // visited selector child but failed
      // Actually mark as tried but not selected:
      activePath.delete(1);
      if (canSee()) {
        activePath.add(2); activePath.add(6); activePath.add(7);
        activeLeaf = "MoveToPlayer";
        moveNpcTowardPlayer();
        document.getElementById("bt-log").textContent = "NPC: видит игрока → MoveToPlayer";
      } else {
        activePath.add(3);
        activeLeaf = "Patrol";
        moveNpcRandom();
        document.getElementById("bt-log").textContent = "NPC: не видит → Patrol";
      }
    }
    drawTree(); drawWorld();
  }

  function moveNpcTowardPlayer() {
    const dx = Math.sign(player.x - npc.x);
    const dy = Math.sign(player.y - npc.y);
    if (Math.abs(player.x - npc.x) > Math.abs(player.y - npc.y)) {
      if (dx !== 0 && canMove(npc.x + dx, npc.y)) npc.x += dx;
      else if (dy !== 0 && canMove(npc.x, npc.y + dy)) npc.y += dy;
    } else {
      if (dy !== 0 && canMove(npc.x, npc.y + dy)) npc.y += dy;
      else if (dx !== 0 && canMove(npc.x + dx, npc.y)) npc.x += dx;
    }
  }
  function moveNpcRandom() {
    const dirs = [[1,0],[-1,0],[0,1],[0,-1]];
    const [dx, dy] = dirs[Math.floor(Math.random() * 4)];
    if (canMove(npc.x + dx, npc.y + dy)) { npc.x += dx; npc.y += dy; }
  }
  function canMove(x, y) { return x >= 0 && x < W && y >= 0 && y < H; }

  function reset() {
    npc = { x: 2, y: 2 };
    player = { x: 8, y: 7 };
    activePath = new Set();
    if (autoTimer) { clearInterval(autoTimer); autoTimer = null; }
    drawTree(); drawWorld();
    document.getElementById("bt-log").textContent = "Нажми «Шаг»";
    document.getElementById("bt-auto").textContent = "Авто";
  }

  document.getElementById("bt-step").addEventListener("click", tick);
  document.getElementById("bt-reset").addEventListener("click", reset);
  document.getElementById("bt-auto").addEventListener("click", (e) => {
    if (autoTimer) {
      clearInterval(autoTimer); autoTimer = null;
      e.target.textContent = "Авто";
      return;
    }
    autoTimer = setInterval(() => {
      // occasionally move player
      if (Math.random() < 0.35) {
        const dirs = [[1,0],[-1,0],[0,1],[0,-1]];
        const [dx, dy] = dirs[Math.floor(Math.random() * 4)];
        const nx = player.x + dx, ny = player.y + dy;
        if (nx >= 0 && nx < W && ny >= 0 && ny < H) { player.x = nx; player.y = ny; }
      }
      tick();
    }, 900);
    e.target.textContent = "Стоп";
  });

  reset();
})();
'@

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
Write-Host "  Открой http://localhost:5000 -> ссылка 'Применения →' в шапке"
Write-Host ""
Write-Host "  4 раздела:"
Write-Host "    - Стек: killer heuristic, стек ply-уровней, симуляция отсечений"
Write-Host "    - Список: Sudoku 4x4, пошаговое решение с откатами"
Write-Host "    - Очередь: играбельная змейка + видимая очередь позиций"
Write-Host "    - Дерево: Behavior Tree + NPC на сетке, кнопки Шаг / Авто"