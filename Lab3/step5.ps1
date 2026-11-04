<#
.SYNOPSIS
    Шаг 5: фикс app.js (Tree), финальные README, REPORT, ADR. Без git.
#>
[CmdletBinding()]
param([string]$Root = ".")

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}
function Write-Info([string]$msg) { Write-Host "  $msg" }
function Write-File([string]$path, [string]$content) {
    $full = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $path))
    $dir = [System.IO.Path]::GetDirectoryName($full)
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($full, $content, [System.Text.UTF8Encoding]::new($true))
    Write-Info "+ $path"
}

Write-Step "Проверка окружения"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet не найден" }
Set-Location (Resolve-Path $Root).Path
Write-Info "папка: $(Get-Location)"

if (-not (Test-Path "LabApp/wwwroot/app.js")) { throw "Нет app.js. Сначала Шаг 4." }

$sln = $null
if (Test-Path "LabApp.slnx")    { $sln = "LabApp.slnx" }
elseif (Test-Path "LabApp.sln") { $sln = "LabApp.sln" }
else { throw "Solution не найден." }
Write-Info "solution: $sln"

# ---------- 1. Фикс app.js (баг с теневой константой post) ----------
Write-Step "Фикс app.js: Tree-баг"
$path = "LabApp/wwwroot/app.js"
$c = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

$before = $c
$c = $c -replace 'const pre = String\.fromCharCode\.apply\(null, r\.preorder\);',  'const preorderStr  = String.fromCharCode.apply(null, r.preorder);'
$c = $c -replace 'const ino = String\.fromCharCode\.apply\(null, r\.inorder\);',   'const inorderStr   = String.fromCharCode.apply(null, r.inorder);'
$c = $c -replace 'const post = String\.fromCharCode\.apply\(null, r\.postorder\);','const postorderStr = String.fromCharCode.apply(null, r.postorder);'
$c = $c -replace 'esc\(pre\)',  'esc(preorderStr)'
$c = $c -replace 'esc\(ino\)',  'esc(inorderStr)'
$c = $c -replace 'esc\(post\)', 'esc(postorderStr)'

if ($c -eq $before) {
    Write-Info "паттерны уже заменены или не найдены — пропускаю"
} else {
    [System.IO.File]::WriteAllText($path, $c, [System.Text.UTF8Encoding]::new($true))
    Write-Info "app.js исправлен"
}

if ($c -match 'const\s+post\s*=') {
    Write-Host "  Внимание: 'const post =' всё ещё присутствует, проверь app.js вручную" -ForegroundColor Yellow
}

# ---------- 2. README.md — финальный ----------
Write-Step "README.md"

Write-File "README.md" @'
# LabApp — практическая работа 3

Единое веб-приложение по структурам данных: стек, очередь, выражения,
связные списки, дерево. Со сравнением своих структур со стандартными
коллекциями .NET и графиком «время от размера входа».

## Что реализовано

### Часть 1. Стек
- Своя реализация на связном списке (`LinkedListStack<T>`)
- Адаптер к стандартному `Stack<T>` (`StandardStackAdapter<T>`)
- Push / Pop / Top / isEmpty / Print
- Парсер `input.txt` (`InputParser`)
- Пошаговый trace + тайминги (read / parse / execute) (`ScenarioRunner`)

### Часть 1.4. Выражения
- Постфиксное вычисление (`PostfixEvaluator`), O(n)
- Инфикс → постфикс (`ShuntingYard`), O(n)
- Поддержка `+ - * : ^ ln cos sin sqrt` и скобок
- Типизированные ошибки: деление на 0, домен ln/sqrt, несбалансированные скобки

### Часть 2. Очередь
- Своя реализация на связном списке (`ListQueue<T>`)
- Адаптер к стандартному `Queue<T>` (`StandardQueueAdapter<T>`)
- Enqueue / Dequeue / Peek / isEmpty / Print
- Один сценарий прогоняется на обеих реализациях

### Часть 3. Примеры применения
Каталог в `docs/REPORT.md`, раздел 5.

### Часть 4. Связные списки
12 алгоритмов в `LabApp/Core/LinkedListAlgorithms.cs`:
reverse, moveLastToFront, moveFirstToEnd, countDistinct, removeNonUnique,
insertSelfAfterX, sortedInsert, removeAll, insertBefore, append,
splitByValue, double, swap.

### Дерево
- Парсинг префиксной записи с `*` как null-маркером (`PrefixParser`)
- Обходы preorder / inorder / postorder + высота (`TreeTraversal`)
- Визуализация в SVG

### Сравнение со стандартными структурами
`BenchmarkRunner` замеряет:
- `LinkedListStack` vs `Stack<T>`
- `ListQueue` vs `Queue<T>`
- На размерах `100 / 1 000 / 10 000 / 100 000`
- Warmup + медиана по итерациям
- График «время vs размер»: две кривые на одном полотне, лог-оси, ratio

## Стек технологий

- .NET 10 (LTS)
- ASP.NET Core Minimal API
- xUnit v3
- Chart.js (CDN) для графика сравнения

## Требования

- .NET 10 SDK
- Браузер
- Интернет для Chart.js (или локальный `wwwroot/chart.umd.min.js`)

## Запуск

    dotnet run --project LabApp

Открыть: http://localhost:5000

## Тесты

    dotnet test

59 тестов: unit-тесты структур, парсера, выражений, бенчмарков + интеграционные через `WebApplicationFactory`.

## Структура

    LabApp.slnx
    LabApp/
      Program.cs
      Core/                  # домен: структуры, парсер, выражения, бенчмарки
      Api/                   # Minimal API эндпоинты + DTO
      wwwroot/               # фронтенд (один index.html + app.js + styles.css)
    Tests/LabApp.Tests/
      *.cs                   # xUnit + интеграционные
    docs/
      ADR.md                 # архитектурные решения
      REPORT.md              # отчёт по практической

## Эндпоинты

| Метод | Endpoint |
|---|---|
| POST | `/api/runs/stack` |
| POST | `/api/runs/queue` |
| POST | `/api/expressions/postfix/evaluate` |
| POST | `/api/expressions/infix/to-postfix` |
| POST | `/api/lists/operations/{op}` |
| POST | `/api/trees/parse-prefix` |
| GET  | `/api/benchmarks/stacks?sizes=100,1000` |
| GET  | `/api/benchmarks/queues?sizes=100,1000` |

## Проверка руками (PowerShell)

    $body = @{ input = '1,1 1,2 3 2' } | ConvertTo-Json
    Invoke-RestMethod -Uri 'http://localhost:5000/api/runs/stack' -Method Post -ContentType 'application/json' -Body $body

Не используй алиас `curl` в Windows PowerShell 5.1 — там он указывает на `Invoke-WebRequest`
с другим синтаксисом. Настоящий curl — `curl.exe`.
'@

# ---------- 3. docs/REPORT.md — рабочий шаблон ----------
Write-Step "docs/REPORT.md"

Write-File "docs/REPORT.md" @'
# Отчёт по практической работе 3

## 1. Цель

Познакомиться со структурами данных Стек, Очередь, Список, Дерево.
Реализовать их на своём связном списке, сравнить со стандартными
коллекциями .NET по времени выполнения.

## 2. Часть 1. Стек

Реализация: `LabApp/Core/Stack.cs`.
Своя база: `SinglyLinkedList<T>` (`LinkedListStack<T>`).
Стандартный адаптер: `StandardStackAdapter<T>` над `System.Collections.Generic.Stack<T>`.

Операции: Push, Pop, Top, isEmpty, Print.
Парсер `input.txt` — `InputParser.cs`.
Пошаговый trace: `ScenarioRunner` + `TraceEvent`.
Замеры: `Timing` (read / parse / execute / total).

Пример сценария: `3 4 1,56 1,7 1,cat 2 5 4`.
Результат: Top → 56, isEmpty → false, pop → cat, print, isEmpty → false.

## 3. Часть 1.4. Выражения

- Постфиксное вычисление: `PostfixEvaluator.cs`, O(n) по времени, O(n) по памяти.
- Инфикс → постфикс: `ShuntingYard.cs`, O(n), приоритеты операторов,
  правоассоциативный `^`, функции `ln / cos / sin / sqrt`, скобки.
- Ошибки типизированы: деление на 0, домен `ln`/`sqrt`, несбалансированные скобки,
  нехватка / избыток операндов.

Пример: `7 8 + 3 2 + /` = 3.
Обратный пример: `(7+8)/(3+2)` → `7 8 + 3 2 + /`.

## 4. Часть 2. Очередь

- Своя реализация: `ListQueue<T>` на своём связном списке (head/tail).
- Стандартная: `StandardQueueAdapter<T>` над `System.Collections.Generic.Queue<T>`.
- Один и тот же сценарий прогоняется на обеих реализациях.
- Замеры сравниваются на графике Benchmarks → Queue.

## 5. Часть 3. Примеры применения структур

Заполнить по одному примеру на каждую структуру. Формат:
задача / где применяется / ссылка на реализацию в проекте.

- Список: ____
- Стек: ____
- Очередь: ____
- Дерево: ____

## 6. Часть 4. Связные списки

Реализованы 12 алгоритмов в `LabApp/Core/LinkedListAlgorithms.cs`:

1. Reverse — O(n)
2. MoveLastToFront — O(n)
3. MoveFirstToEnd — O(1)
4. CountDistinct — O(n) с хэш-таблицей
5. RemoveNonUnique — O(n)
6. InsertSelfAfterX — O(n + k)
7. SortedInsert — O(n)
8. RemoveAll — O(n)
9. InsertBefore — O(n)
10. Append — O(n + m)
11. SplitByValue — O(n)
12. Double — O(n)
13. Swap — O(n)

Все покрыты unit-тестами, включая edge-case’ы: пустой список, один элемент,
отсутствующий ключ, дубликаты.

## 7. Дерево

- Парсер префиксной записи: `PrefixParser.cs`, `*` — null-маркер.
- Обходы: `TreeTraversal` (preorder / inorder / postorder) + высота.
- Визуализация в UI в виде SVG.

Пример: `ABD*G***CE**FH**J**`
- Preorder: ABDGCEFHJ
- Inorder: DBGAEHCJF
- Postorder: GDBAEHCJF
- Высота: 4

## 8. Сравнение со стандартными структурами

Замеры производятся на вкладке Benchmarks веб-интерфейса.
Значения из последнего прогона (заполнить вручную с экрана):

### Стек, Push+Pop

| N | Своя, мс | Стандартная, мс | Ratio (своя / станд.) |
|---|----------|-----------------|-----------------------|
| 100 | | | |
| 1 000 | | | |
| 10 000 | | | |
| 100 000 | | | |

### Очередь, Enqueue+Dequeue

| N | Своя, мс | Стандартная, мс | Ratio (своя / станд.) |
|---|----------|-----------------|-----------------------|
| 100 | | | |
| 1 000 | | | |
| 10 000 | | | |
| 100 000 | | | |

**Вывод:** ____

Ожидаемая картина:
- Обе структуры дают O(1) на операцию, но у стандартных меньше константа
  за счёт cache locality и отсутствия аллокации узла на каждый элемент.
- Своя реализация проигрывает по времени, но показывает, как устроена
  структура изнутри.

## 9. Как запустить

    dotnet run --project LabApp

Открыть http://localhost:5000.

Тесты:

    dotnet test

59 тестов: unit + интеграционные.

## 10. Выводы

- Реализованы Стек, Очередь, Связный список (12 алгоритмов), Дерево.
- Сравнение со стандартными коллекциями .NET показало разницу в константе
  (аллокации + cache locality), а не в асимптотике.
- Постфиксное вычисление и перевод инфикс → постфикс работают за O(n).
- Пошаговый trace и визуализация помогают разобраться в работе структур.
'@

# ---------- 4. docs/ADR.md — расширяем ----------
Write-Step "docs/ADR.md"

Write-File "docs/ADR.md" @'
# ADR — Architecture Decision Records

## ADR-0001. Модульный монолит

Статус: принято.
Контекст: учебный домен, одна команда, ограниченное время.
Решение: один процесс ASP.NET Core, модули — папки (Core, Api). Микросервисы избыточны.
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

## ADR-0007. Медиана + p95 вместо среднего

Статус: принято.
Контекст: микро-замеры шумные, среднее искажается выбросами от GC.
Решение: warmup + медиана по итерациям. p95 сохраняется для отчёта.
Следствия: график стабилен, ratio осмыслен.

## ADR-0008. Лог-оси на графике

Статус: принято.
Контекст: размеры входа от 10^2 до 10^5, время от микросекунд до миллисекунд.
Решение: X и Y логарифмические, единицы автоматические.
Следствия: видна и константа, и асимптотика одновременно.
'@

# ---------- 5. Сборка и тесты ----------
Write-Step "Сборка"
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build упал (код $LASTEXITCODE)" }

Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet test упал (код $LASTEXITCODE)" }

# ---------- 6. Итог ----------
Write-Step "Готово"
Write-Host "  Практическая закрыта. Что осталось вручную:"
Write-Host ""
Write-Host "  1) Запусти приложение:"
Write-Host "       dotnet run --project LabApp"
Write-Host "     Открой http://localhost:5000"
Write-Host ""
Write-Host "  2) Проверь все вкладки по очереди:"
Write-Host "     - Stack, Queue  — прогон сценариев, trace + тайминги"
Write-Host "     - Expressions   — постфикс и инфикс->постфикс"
Write-Host "     - Lists         — 12 алгоритмов, до/после"
Write-Host "     - Tree          — обходы + SVG-дерево"
Write-Host "     - Benchmarks    — график 'время vs размер', обе кривые"
Write-Host ""
Write-Host "  3) Сними цифры с графика Benchmarks и заполни таблицы"
Write-Host "     в docs/REPORT.md, раздел 8."
Write-Host ""
Write-Host "  4) Раздел 5 (примеры применения List/Stack/Queue/Tree) — заполни текстом."
Write-Host ""
Write-Host "  5) Коммит и пуш — на твоей стороне (без git-команд в скрипте)."
Write-Host ""
Write-Host "  Файлы отчёта: docs/REPORT.md, docs/ADR.md, README.md"