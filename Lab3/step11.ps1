<#
.SYNOPSIS
    Фикс Snake: правильный старт + корректная проверка столкновений.
#>
[CmdletBinding()]
param([string]$Root = ".")

$ErrorActionPreference = "Stop"
function Write-Step([string]$msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Info([string]$msg) { Write-Host "  $msg" }

Set-Location (Resolve-Path $Root).Path
$path = "LabApp/wwwroot/apply.js"
if (-not (Test-Path $path)) { throw "Нет apply.js." }

$c = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$before = $c

# --- Fix 1: правильный порядок начальных сегментов ---
$c = $c.Replace(
    'queue = [{ x: 5, y: 7 }, { x: 4, y: 7 }, { x: 3, y: 7 }];',
    'queue = [{ x: 3, y: 7 }, { x: 4, y: 7 }, { x: 5, y: 7 }];'
)

# --- Fix 2: не считать хвост, который сейчас сдвинется ---
$oldStep = @'
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
'@

$newStep = @'
  function step() {
    if (!running || paused) return;
    dir = nextDir;
    const head = queue[queue.length - 1];
    const nh = { x: head.x + dir.x, y: head.y + dir.y };
    if (nh.x < 0 || nh.x >= COLS || nh.y < 0 || nh.y >= ROWS) { end(); return; }

    const willEat = (nh.x === food.x && nh.y === food.y);
    // Если не едим — хвост уйдёт на этом шаге, его позицию занимать можно.
    const segmentsToCheck = willEat ? queue : queue.slice(1);
    if (segmentsToCheck.some(s => s.x === nh.x && s.y === nh.y)) { end(); return; }

    queue.push(nh); // enqueue
    if (willEat) {
      score++;
      placeFood();
    } else {
      queue.shift(); // dequeue
    }
    draw(); updateSide();
  }
'@

$c = $c.Replace($oldStep, $newStep)

if ($c -eq $before) {
    throw "Ни одна замена не сработала. Возможно, apply.js уже изменён вручную — проверь."
}

[System.IO.File]::WriteAllText($path, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "apply.js исправлен (старт + проверка хвоста)"

# Проверим, что замены реально применились
$after = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
if ($after -match 'queue = \[\{ x: 5, y: 7 \}, \{ x: 4, y: 7 \}, \{ x: 3, y: 7 \}\]') {
    Write-Host "  Внимание: старый порядок всё ещё в файле — проверь вручную" -ForegroundColor Yellow
}
if (-not ($after -match 'segmentsToCheck')) {
    Write-Host "  Внимание: проверка хвоста не применилась — проверь вручную" -ForegroundColor Yellow
}

Write-Step "Готово"
Write-Host "  Перезапусти: dotnet run --project LabApp"
Write-Host "  http://localhost:5000/apply.html -> раздел 'Очередь' -> Старт"
Write-Host ""
Write-Host "  Ожидаемо: змейка идёт вправо, кушает яблоки, растёт,"
Write-Host "  проигрывает только при выходе за границу или укусе себя."