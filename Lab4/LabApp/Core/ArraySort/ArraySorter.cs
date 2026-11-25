using System.Diagnostics;

namespace LabApp.Core.ArraySort;

/// <summary>Итог одного запуска внутренней сортировки массива (задание 1).</summary>
public sealed class ArraySortRun
{
    public required IArraySortAlgorithm Algorithm { get; init; }
    public required bool Ascending { get; init; }
    public required ArrayEntry[] Elements { get; init; }   // исходный (несортированный) массив
    public required ArraySortTrace Trace { get; init; }
}

/// <summary>Результат одного замера времени (по схеме лабораторной №1: прогрев + несколько прогонов).</summary>
public sealed record ArrayBenchResult(
    string AlgorithmId,
    string AlgorithmTitle,
    int ElementCount,
    string Source,        // описание сгенерированного массива
    int Runs,
    double AvgMs,
    double MinMs,
    double MaxMs,
    long Comparisons,
    long Moves,
    int Passes,
    IReadOnlyList<double> Times);   // время каждого прогона, мс — для истории

/// <summary>
/// Фасад внутренней сортировки: проверяет массив пользователя, запускает выбранный алгоритм,
/// строит трассировку и мерит время по схеме «прогрев + N прогонов Stopwatch».
/// </summary>
public sealed class ArraySorter
{
    /// <summary>Предел чисел для пошаговой визуализации: защита от гигантской трассировки.</summary>
    public const int MaxTraceElements = 100;

    /// <summary>
    /// Предел чисел для замеров времени. Квадратичные алгоритмы растут как n² —
    /// на больших объёмах точка меряется долго (кнопка «Остановить» прерывает серию),
    /// поэтому объём ограничен, но даём запас до 100 000 для быстрых O(n·log n).
    /// </summary>
    public const int MaxBenchElements = 100_000;

    /// <summary>Сколько прогонов-прогревов делаем перед замерами (как в лабораторной №1).</summary>
    public const int WarmupRuns = 3;

    /// <summary>Минимальное и максимальное случайное число в сгенерированных массивах.</summary>
    public const int RandomMin = 1;
    public const int RandomMax = 999;

    private readonly Dictionary<string, IArraySortAlgorithm> _algorithms;

    public ArraySorter(IEnumerable<IArraySortAlgorithm> algorithms)
    {
        _algorithms = algorithms.ToDictionary(a => a.Id);
    }

    /// <summary>Список доступных алгоритмов (для карточек выбора в интерфейсе).
    /// treeModeMax — лимит режима визуализации «Дерево» (null — у алгоритма дерева нет).</summary>
    public IReadOnlyList<ArraySortMethodInfo> ListMethods() =>
        _algorithms.Values
            .Select(a => new ArraySortMethodInfo(
                a.Id, a.Title, a.Group, a.Badge, a.ShortInfo, a.PassesLabel, a.MemorySlots,
                a is TournamentSort ? TournamentSort.TreeModeMaxElements : null))
            .ToList();

    /// <summary>
    /// Демонстрация: трассировка выбранного алгоритма на массиве пользователя.
    /// values — числа в исходном порядке; алгоритм сортирует копию, исходный массив не меняется.
    /// </summary>
    public ArraySortRun Run(int[] values, string methodId, bool ascending)
    {
        if (!_algorithms.TryGetValue(methodId, out var algo))
            throw new TableFormatException($"Неизвестный алгоритм сортировки: «{methodId}».");
        if (values.Length == 0)
            throw new TableFormatException("Массив пустой — сортировать нечего. Введи хотя бы одно число.");
        if (values.Length > MaxTraceElements)
            throw new TableFormatException(
                $"Для пошаговой визуализации берём до {MaxTraceElements} чисел (здесь {values.Length}). " +
                "Большие объёмы сортируйте на панели «Замеры времени».");

        bool descending = !ascending;

        // Демонстрация: сортируем копию массива с пошаговой трассировкой.
        var work = MakeEntries(values); // Id = исходная позиция числа
        var builder = new ArraySortTraceBuilder(work, active: true);
        algo.Sort(work, descending, builder);
        var trace = builder.Build(work.Select(w => w.Id).ToArray());

        return new ArraySortRun
        {
            Algorithm = algo,
            Ascending = ascending,
            Elements = MakeEntries(values), // исходный порядок — для панели «Исходный массив»
            Trace = trace,
        };
    }

    /// <summary>
    /// Замер времени сортировки по схеме лабораторной №1: сначала прогревочные прогоны,
    /// затем runs замеров с Stopwatch; в ответ идут среднее, минимум, максимум и все прогоны.
    /// Замер независим от настроек демонстрации: объём массива задаётся прямо здесь.
    /// Фиксированный сид: один и тот же объём даёт один и тот же массив — замеры воспроизводимы.
    /// </summary>
    public ArrayBenchResult Benchmark(string algorithmId, int runs, int elementCount)
    {
        if (!_algorithms.TryGetValue(algorithmId, out var algo))
            throw new TableFormatException($"Неизвестный алгоритм сортировки: «{algorithmId}».");
        runs = Math.Clamp(runs, 1, 50);
        int n = Math.Clamp(elementCount, 1, MaxBenchElements);

        var all = Generate(n, seed: n * 7919);

        // Прогрев: разогреваем JIT и кэши, чтобы замеры были честными.
        // На больших объёмах одного прогрева достаточно — иначе он сам занимает заметное время.
        int warmups = n > 10_000 ? 1 : WarmupRuns;
        for (int w = 0; w < warmups; w++)
            algo.SortQuiet(MakeEntries(all), descending: false);

        var times = new double[runs];
        ArraySortStats stats = new(0, 0, 0);
        for (int r = 0; r < runs; r++)
        {
            var work = MakeEntries(all); // свежий массив на каждый прогон — сортировка «на месте»
            var sw = Stopwatch.StartNew();
            stats = algo.SortQuiet(work, descending: false);
            sw.Stop();
            times[r] = sw.Elapsed.TotalMilliseconds;
        }

        return new ArrayBenchResult(
            algo.Id, algo.Title, n, $"сгенерированный массив из {n} чисел", runs,
            AvgMs: times.Average(),
            MinMs: times.Min(),
            MaxMs: times.Max(),
            stats.Comparisons, stats.Moves, stats.Passes,
            times.ToList());
    }

    /// <summary>
    /// Случайный массив для демонстрации: детерминированный генератор с фиксированным сидом —
    /// с одним сидом получается один и тот же массив, поэтому его можно пересортировать
    /// другим алгоритмом или в другом порядке.
    /// </summary>
    public (int[] Values, int Seed) RandomArray(int count, int? seed)
    {
        int usedSeed = seed ?? Random.Shared.Next();
        return (Generate(Math.Clamp(count, 1, MaxTraceElements), usedSeed), usedSeed);
    }

    /// <summary>Генератор массива из count чисел с фиксированным сидом (воспроизводимые замеры).</summary>
    public static int[] Generate(int count, int seed)
    {
        var rnd = new Random(seed);
        var values = new int[count];
        for (int i = 0; i < values.Length; i++)
            values[i] = rnd.Next(RandomMin, RandomMax + 1);
        return values;
    }

    /// <summary>Разбор массива, введённого пользователем: числа через пробел, запятую или с новой строки.</summary>
    public static int[] ParseValues(string text)
    {
        var tokens = (text ?? "").Split([' ', ',', ';', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var values = new List<int>(tokens.Length);
        foreach (string t in tokens)
        {
            if (!int.TryParse(t.Trim(), out int v))
                throw new TableFormatException(
                    $"«{t}» — не целое число. Вводи числа через пробел, запятую или с новой строки.");
            values.Add(v);
        }
        return [.. values];
    }

    private static ArrayEntry[] MakeEntries(IReadOnlyList<int> values)
    {
        var result = new ArrayEntry[values.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = new ArrayEntry(i, values[i]);
        return result;
    }
}
