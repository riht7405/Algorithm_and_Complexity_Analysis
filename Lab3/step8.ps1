<#
.SYNOPSIS
    Шаг 8: добавление вкладки "Примеры" с задачами и логикой их решения.
.DESCRIPTION
    Добавляет вкладку в index.html, стили карточек в styles.css
    и HTML-контент с задачами для Стека, Списка, Очереди и Дерева.
#>
[CmdletBinding()]
param([string]$Root = ".")

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Info([string]$msg) { Write-Host "  $msg" }

Set-Location (Resolve-Path $Root).Path

if (-not (Test-Path "LabApp/wwwroot/index.html")) { throw "Нет index.html. Сначала предыдущие шаги." }

# ---------- 1. index.html: добавляем кнопку вкладки ----------
Write-Step "index.html: добавляем кнопку 'Примеры'"
$htmlPath = "LabApp/wwwroot/index.html"
$c = [System.IO.File]::ReadAllText($htmlPath, [System.Text.Encoding]::UTF8)

$c = $c.Replace(
    '<button class="tab" data-tab="bench">Benchmarks</button>' + "`r`n" + '    </nav>',
    '<button class="tab" data-tab="bench">Benchmarks</button>' + "`r`n" + '      <button class="tab" data-tab="examples">Примеры</button>' + "`r`n" + '    </nav>'
)
# Если переводы строк были \n, а не \r\n
$c = $c.Replace(
    '<button class="tab" data-tab="bench">Benchmarks</button>' + "`n" + '    </nav>',
    '<button class="tab" data-tab="bench">Benchmarks</button>' + "`n" + '      <button class="tab" data-tab="examples">Примеры</button>' + "`n" + '    </nav>'
)

# ---------- 2. index.html: добавляем секцию с задачами ----------
Write-Step "index.html: секция с задачами"

$examplesSection = @'
  <!-- Examples -->
  <section id="panel-examples" class="panel">
    <h2>Нестандартные применения структур</h2>
    <p class="muted">Реальные задачи из индустрии, где эти структуры данных играют ключевую роль.</p>

    <div class="task-card">
      <h3>1. Стек: Стековая эвристика в шахматных движках</h3>
      <div class="task-row">
        <div class="task-col">
          <strong>Условие:</strong>
          <p>Шахматный движок перебирает миллионы позиций. Нужно быстро находить ходы, которые с наибольшей вероятностью приведут к отсечению (beta-cutoff), чтобы не перебирать бесполезные ветки дерева и сэкономить время.</p>
        </div>
        <div class="task-col">
          <strong>Логика решения:</strong>
          <p>Применяется <i>Killer Heuristic</i>. Используется стек, где каждый уровень соответствует глубине поиска (ply). В каждом узле стека хранятся 1–2 «тихих» хода (не взятия), которые ранее уже приводили к отсечению на этой же глубине. Поскольку поиск рекурсивный, стек идеально ложится на структуру вызовов: мы заходим вглубь — кладем новый уровень, выходим — снимаем. Это позволяет запоминать «убийственные» ходы локально для каждой глубины.</p>
        </div>
      </div>
    </div>

    <div class="task-card">
      <h3>2. Список: Dancing Links (Алгоритм X) для Судоку</h3>
      <div class="task-row">
        <div class="task-col">
          <strong>Условие:</strong>
          <p>Есть задача точного покрытия (Exact Cover), например, Судоку. Нужно быстро перебирать варианты с возможностью отката (backtracking) за минимальное время.</p>
        </div>
        <div class="task-col">
          <strong>Логика решения:</strong>
          <p>Используется структура Dancing Links (танцующие ссылки) на основе двусвязных списков. Матрица ограничений Судоку представляется в виде узлов, связанных по горизонтали и вертикали. Когда алгоритм делает ход, он «вырезает» выбранный узел и все конфликтующие с ним из списка (удаляет связи). Если решение зашло в тупик, узлы «вставляются» обратно (откат). Это позволяет делать откат за O(1) и очень быстро перебирать миллионы комбинаций.</p>
        </div>
      </div>
    </div>

    <div class="task-card">
      <h3>3. Очередь: Игра «Змейка»</h3>
      <div class="task-row">
        <div class="task-col">
          <strong>Условие:</strong>
          <p>Управлять змейкой на поле. Змейка движется, ест яблоки, растет и не должна врезаться в свой хвост. Каждый шаг — это сдвиг всей змейки на одну клетку.</p>
        </div>
        <div class="task-col">
          <strong>Логика решения:</strong>
          <p>Тело змейки — это очередь. В начале очереди находится хвост, в конце — голова. При каждом шаге игрок выбирает направление, мы добавляем новую координату головы в конец очереди (<code>enqueue</code>), а старую координату хвоста удаляем из начала (<code>dequeue</code>). Если змейка съедает яблоко, мы добавляем голову, но <i>не</i> удаляем хвост — змейка растет. Очередь гарантирует, что все сегменты движутся друг за другом.</p>
        </div>
      </div>
    </div>

    <div class="task-card">
      <h3>4. Дерево: Behavior Tree (Дерево поведения) для AI</h3>
      <div class="task-row">
        <div class="task-col">
          <strong>Условие:</strong>
          <p>Описать сложное поведение игрового NPC (патрулировать, заметить врага, атаковать, убежать при низком HP). Код не должен превращаться в «лапшу» из if-else.</p>
        </div>
        <div class="task-col">
          <strong>Логика решения:</strong>
          <p>Используется Дерево поведения (Behavior Tree). Это иерархическое дерево, где листья — это действия (например, «Двигаться к точке», «Выстрелить»), а внутренние узлы — это логические операторы (Селектор, Последовательность). Селектор пробует дочерние узлы слева направо, пока один не завершится успехом. Последовательность выполняет детей по порядку, пока все не выполнятся. Дерево обходится рекурсивно на каждом «тике» игры, выбирая актуальную ветку поведения.</p>
        </div>
      </div>
    </div>
  </section>
'@

# Вставляем перед закрывающим </main>
$c = $c.Replace("</main>", $examplesSection + "`r`n</main>")
$c = $c.Replace("</main>", $examplesSection + "`n</main>") # страховка для \n

[System.IO.File]::WriteAllText($htmlPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "index.html обновлён"

# ---------- 3. styles.css: стили карточек задач ----------
Write-Step "styles.css: стили для карточек"

$cssPath = "LabApp/wwwroot/styles.css"
$css = [System.IO.File]::ReadAllText($cssPath, [System.Text.Encoding]::UTF8)

$append = @'

/* ---- Задачки / Примеры ---- */
.task-card {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 8px;
  padding: 1.25rem;
  margin-bottom: 1.5rem;
}
.task-card h3 {
  margin-top: 0;
  color: var(--accent);
  font-size: 1.15rem;
}
.task-row {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
}
.task-col {
  flex: 1;
  min-width: 280px;
}
.task-col strong {
  display: block;
  margin-bottom: .25rem;
  color: var(--fg);
  font-size: 0.95rem;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}
.task-col p {
  margin-top: 0;
  line-height: 1.6;
  color: #374151;
  font-size: 0.95rem;
}
'@

[System.IO.File]::WriteAllText($cssPath, $css + $append, [System.Text.UTF8Encoding]::new($true))
Write-Info "styles.css обновлён"

# ---------- 4. Сборка и тесты ----------
Write-Step "Сборка"
$sln = if (Test-Path "LabApp.slnx") { "LabApp.slnx" } else { "LabApp.sln" }
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build упал" }

Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet test упал" }

Write-Step "Готово"
Write-Host "  Запускай: dotnet run --project LabApp"
Write-Host "  В шапке появилась вкладка 'Примеры'."
Write-Host "  В ней — 4 карточки с задачами и логикой их решения."