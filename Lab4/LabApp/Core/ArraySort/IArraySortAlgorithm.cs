namespace LabApp.Core.ArraySort;

/// <summary>
/// Один алгоритм внутренней сортировки массива чисел (задание 1). Новый алгоритм добавляется
/// реализацией этого интерфейса и одной строкой регистрации в Program.cs — интерфейс
/// пользователя, API и замеры времени подхватят его автоматически.
/// Алгоритмы делятся на группы (Group): «Квадратичные и субквадратичные» и «Усовершенствованные» —
/// в интерфейсе карточки группируются по этому полю, как в классификации задания.
/// </summary>
public interface IArraySortAlgorithm
{
    /// <summary>Идентификатор алгоритма (его передаёт интерфейс пользователя).</summary>
    string Id { get; }

    /// <summary>Название для пользователя.</summary>
    string Title { get; }

    /// <summary>Группа по классификации задания: «Квадратичные и субквадратичные» и т.п.</summary>
    string Group { get; }

    /// <summary>Короткая пометка о сложности для карточки выбора (например «O(n²)»).</summary>
    string Badge { get; }

    /// <summary>Короткое объяснение метода «для 6-классника».</summary>
    string ShortInfo { get; }

    /// <summary>Как назвать третий счётчик в статистике: «проходов», «вставок», «слияний».</summary>
    string PassesLabel { get; }

    /// <summary>Сколько значений алгоритм держит в памяти одновременно (для бейджа карточки).</summary>
    int MemorySlots { get; }

    /// <summary>
    /// Сортировка с пошаговой трассировкой: переставляет элементы массива на месте
    /// и пишет шаги в trace. a — копия массива для показа; алгоритм не должен менять другие данные.
    /// </summary>
    void Sort(ArrayEntry[] a, bool descending, ArraySortTraceBuilder trace);

    /// <summary>
    /// Тихая сортировка для замеров времени: тот же код, но шаги не собираются —
    /// остаются только счётчики. a сортируется на месте.
    /// </summary>
    ArraySortStats SortQuiet(ArrayEntry[] a, bool descending)
    {
        var trace = new ArraySortTraceBuilder(a, active: false);
        Sort(a, descending, trace);
        return new ArraySortStats(trace.Comparisons, trace.Moves, trace.Passes);
    }
}

/// <summary>Описание алгоритма внутренней сортировки для списка в интерфейсе.</summary>
public sealed record ArraySortMethodInfo(
    string Id, string Title, string Group, string Badge, string ShortInfo, string PassesLabel, int MemorySlots);

/// <summary>Заголовки групп алгоритмов по классификации задания.</summary>
public static class ArraySortGroups
{
    public const string Quadratic = "Квадратичные и субквадратичные";
    public const string Advanced = "Усовершенствованные";
}
