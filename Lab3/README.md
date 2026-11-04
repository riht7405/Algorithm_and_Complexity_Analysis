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