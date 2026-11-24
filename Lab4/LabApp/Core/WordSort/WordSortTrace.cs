namespace LabApp.Core.WordSort;

/// <summary>Слово текста с исходным номером: по Id визуализация находит слово в исходном тексте.</summary>
public sealed record WordEntry(int Id, string Text);

/// <summary>Снимок одной «корзины» ABC-сортировки на момент шага. Letter — буква корзины,
/// для слов, которые уже целиком «уложились» в префикс (короче текущей глубины), — «✓».</summary>
public sealed record BucketSnapshot(string Letter, int[] Ids);

/// <summary>Тип шага визуализации сортировки слов.</summary>
public enum WordStepKind
{
    PhaseStart, // «заголовок» этапа
    PassStart,  // начало прохода: новый кусок (QuickSort) или разбор корзины (ABC)
    Compare,    // сравнили два слова (или слово с опорным, или буквы при поиске корзины)
    Swap,       // переставили два слова местами
    Shift,      // сдвиг слова на позицию вправо (вставки на малых кусках)
    Place,      // слово положили в корзину (ABC)
    Collect,    // собрали корзины обратно в массив (ABC)
    Note,       // пояснение без перестановок (например «все слова куска одинаковые»)
    Count,      // подсчёт вхождений: прошли кусок одинаковых слов
    Done        // сортировка закончена
}

/// <summary>Один шаг сортировки слов: что произошло, что подсвечено, как выглядит массив/корзины, строка журнала.
/// Все ссылки на слова — по их Id (позиция в массиве меняется, Id — нет).</summary>
public sealed class WordSortStep
{
    public int Index { get; init; }
    public WordStepKind Kind { get; init; }
    public string Phase { get; init; } = string.Empty;
    public int Pass { get; init; }
    public string? PassTitle { get; init; }
    public string Desc { get; init; } = string.Empty;      // объяснение простым языком
    public string? LogLine { get; init; }                  // строка журнала сравнений/перестановок
    public string? LogKind { get; init; }                  // compare | move | phase | count | done
    public int[]? ArrayIds { get; init; }                  // снимок порядка слов (null — порядок не менялся)
    public int[]? Highlights { get; init; }                // слова, подсвеченные на этом шаге (Id)
    public int? PivotId { get; init; }                     // опорное слово (QuickSort)
    public int[]? RangeIds { get; init; }                  // текущий рабочий кусок (Id)
    public int[]? Placed { get; init; }                    // слова, уже стоящие на финальных местах (Id)
    public int[] MemoryIds { get; init; } = [];            // какие слова «в памяти» на этом шаге
    public string MemoryNote { get; init; } = string.Empty;
    public List<BucketSnapshot>? Buckets { get; init; }    // корзины текущего куска (ABC)
    public long Comparisons { get; init; }
    public long Moves { get; init; }
    public int Passes { get; init; }
}

/// <summary>Полная трассировка одного запуска сортировки слов.</summary>
public sealed class WordSortTrace
{
    public List<WordSortStep> Steps { get; init; } = [];
    public int[] FinalOrder { get; set; } = [];   // Id слов в отсортированном порядке
    public long TotalComparisons { get; set; }
    public long TotalMoves { get; set; }
    public int TotalPasses { get; set; }
}

/// <summary>Итоговые счётчики тихого прогона (для замеров времени и экспериментов).</summary>
public sealed record WordSortStats(long Comparisons, long Moves, int Passes);
