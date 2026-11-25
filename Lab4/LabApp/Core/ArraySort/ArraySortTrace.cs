namespace LabApp.Core.ArraySort;

/// <summary>
/// Число массива с исходным номером: по Id визуализация находит элемент в исходном массиве,
/// даже когда он переставлен на другую позицию. Value — само число, оно не меняется.
/// </summary>
public sealed record ArrayEntry(int Id, int Value);

/// <summary>Тип шага визуализации внутренней сортировки.</summary>
public enum ArrayStepKind
{
    PhaseStart, // «заголовок» этапа
    PassStart,  // начало прохода: новый выбор минимума, вставка, слияние серий
    Compare,    // сравнили два числа
    Swap,       // переставили два числа местами
    Shift,      // сдвиг числа на позицию (вставки: освобождаем место для «взятого»)
    Write,      // записали число в буфер (слияние)
    CopyBack,   // переписали буфер обратно в массив (слияние: конец прохода)
    Note,       // пояснение без перестановок (например «минимум уже на месте»)
    Place,      // «взятое» число опустилось на своё место (вставки; не сравнение и не обмен)
    Done        // сортировка закончена
}

/// <summary>Один шаг сортировки массива: что произошло, что подсвечено, как выглядит массив/буфер, строка журнала.
/// Все ссылки на числа — по их Id (позиция в массиве меняется, Id — нет).</summary>
public sealed class ArraySortStep
{
    public int Index { get; init; }
    public ArrayStepKind Kind { get; init; }
    public string Phase { get; init; } = string.Empty;
    public int Pass { get; init; }
    public string? PassTitle { get; init; }
    public string Desc { get; init; } = string.Empty;      // объяснение простым языком
    public string? LogLine { get; init; }                  // строка журнала сравнений/перестановок
    public string? LogKind { get; init; }                  // compare | move | phase | note | done
    public int[]? ArrayIds { get; init; }                  // снимок порядка чисел (null — порядок не менялся)
    public int[]? Highlights { get; init; }                // числа, подсвеченные на этом шаге (Id)
    public int? MarkId { get; init; }                      // особое число: текущий минимум (SelectSort)
    public int[]? RangeIds { get; init; }                  // текущий рабочий кусок (Id)
    public int[]? Placed { get; init; }                    // числа, уже стоящие на финальных местах (Id)
    public int[]? BufferIds { get; init; }                 // снимок буфера слияния (null — буфер не используется)
    /// <summary>Серии, которые сейчас сливает MergeSort: SeriesA — левая, SeriesB — правая.
    /// Пустой список — серии сброшены (между парами и на старте прохода); null — алгоритм
    /// серии не размечает (SelectSort, InsertSort). Серия без пары: SeriesA заполнена,
    /// SeriesB пустая.</summary>
    public int[]? SeriesA { get; init; }
    public int[]? SeriesB { get; init; }
    public int[] MemoryIds { get; init; } = [];            // какие числа «в памяти» на этом шаге
    public string MemoryNote { get; init; } = string.Empty;
    public long Comparisons { get; init; }
    public long Moves { get; init; }
    public int Passes { get; init; }
}

/// <summary>Полная трассировка одного запуска внутренней сортировки.</summary>
public sealed class ArraySortTrace
{
    public List<ArraySortStep> Steps { get; init; } = [];
    public int[] FinalOrder { get; set; } = [];   // Id чисел в отсортированном порядке
    public long TotalComparisons { get; set; }
    public long TotalMoves { get; set; }
    public int TotalPasses { get; set; }
}

/// <summary>Итоговые счётчики тихого прогона (для замеров времени и экспериментов).</summary>
public sealed record ArraySortStats(long Comparisons, long Moves, int Passes);
