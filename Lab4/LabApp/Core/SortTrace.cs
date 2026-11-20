namespace LabApp.Core;

/// <summary>Тип шага визуализации.</summary>
public enum StepKind
{
    PhaseStart, // «заголовок» этапа
    PassStart,  // начало прохода
    Split,      // переписали запись из файла в ленту (разбивка/распределение)
    Compare,    // сравнили значения в памяти
    Move,       // переписали запись-результат в файл
    Flush,      // дописали остаток серии без сравнений
    Done        // сортировка закончена
}

/// <summary>Снимок состояния одного файла-ленты на момент шага.</summary>
public sealed record TapeSnapshot(string Name, string Role, int[] Ids, int Cursor, int[][]? Runs);

/// <summary>Один шаг сортировки: что произошло, что в памяти, как выглядят файлы, строка журнала.</summary>
public sealed class SortStep
{
    public int Index { get; init; }
    public StepKind Kind { get; init; }
    public string Phase { get; init; } = string.Empty;
    public int Pass { get; init; }
    public string? PassTitle { get; init; }
    public string Desc { get; init; } = string.Empty;      // объяснение простым языком
    public string? LogLine { get; init; }                  // строка журнала сравнений/перестановок
    public string? LogKind { get; init; }                  // compare | move | phase | done
    public int[] MemoryIds { get; init; } = [];            // какие записи лежат в памяти (Id строк)
    public string MemoryNote { get; init; } = string.Empty;
    public List<TapeSnapshot>? Snap { get; init; }         // null — состояние файлов не изменилось
    public int Comparisons { get; init; }
    public int Moves { get; init; }
    public int Passes { get; init; }
}

/// <summary>Полная трассировка одного запуска внешней сортировки.</summary>
public sealed class SortTrace
{
    public List<SortStep> Steps { get; init; } = [];
    public List<int> FinalOrder { get; set; } = [];
    public string? ResultFileName { get; set; }
    public int TotalComparisons { get; set; }
    public int TotalMoves { get; set; }
    public int TotalPasses { get; set; }
}
