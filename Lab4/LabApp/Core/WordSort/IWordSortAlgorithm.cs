namespace LabApp.Core.WordSort;

/// <summary>
/// Один алгоритм сортировки слов. Новый алгоритм добавляется реализацией этого интерфейса
/// и одной строкой регистрации в Program.cs — интерфейс пользователя, API и замеры времени
/// подхватят его автоматически.
/// </summary>
public interface IWordSortAlgorithm
{
    /// <summary>Идентификатор алгоритма (его передаёт интерфейс пользователя).</summary>
    string Id { get; }

    /// <summary>Название для пользователя.</summary>
    string Title { get; }

    /// <summary>Короткая пометка о сложности для карточки выбора (например «O(n·log n)»).</summary>
    string Badge { get; }

    /// <summary>Короткое объяснение метода «для 6-классника».</summary>
    string ShortInfo { get; }

    /// <summary>Как назвать третий счётчик в статистике: «кусков», «уровней букв» и т.п.</summary>
    string PassesLabel { get; }

    /// <summary>Сколько значений алгоритм держит в памяти одновременно (для бейджа карточки).</summary>
    int MemorySlots { get; }

    /// <summary>
    /// Сортировка с пошаговой трассировкой: переставляет words на месте и пишет шаги в trace.
    /// words — копия списка слов для показа; алгоритм не должен менять другие данные.
    /// </summary>
    void Sort(WordEntry[] words, bool descending, WordSortTraceBuilder trace);

    /// <summary>
    /// Тихая сортировка для замеров времени: тот же код, но шаги не собираются —
    /// остаются только счётчики. words сортируется на месте.
    /// </summary>
    WordSortStats SortQuiet(WordEntry[] words, bool descending)
    {
        var trace = new WordSortTraceBuilder(words, active: false);
        Sort(words, descending, trace);
        return new WordSortStats(trace.Comparisons, trace.Moves, trace.Passes);
    }
}

/// <summary>Описание алгоритма сортировки слов для списка в интерфейсе.</summary>
public sealed record WordSortMethodInfo(
    string Id, string Title, string Badge, string ShortInfo, string PassesLabel, int MemorySlots);
