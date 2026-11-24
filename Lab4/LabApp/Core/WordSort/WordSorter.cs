using System.Diagnostics;
using LabApp.Core.Texts;

namespace LabApp.Core.WordSort;

/// <summary>Одна запись подсчёта: слово и сколько раз оно встретилось в отсортированном массиве.</summary>
public sealed record WordCount(int WordId, string Word, int Count);

/// <summary>Итог одного запуска сортировки слов (задание 3).</summary>
public sealed class WordSortRun
{
    public required string FileName { get; init; }        // имя файла или подпись случайного текста
    public required IWordSortAlgorithm Algorithm { get; init; }
    public required bool Ascending { get; init; }
    public required int WordCount { get; init; }          // сколько слов сортировалось
    public required WordEntry[] Words { get; init; }      // исходный (несортированный) список слов
    public required WordSortTrace Trace { get; init; }
    public required IReadOnlyList<WordCount> Counts { get; init; } // подсчёт по всему тексту
    public required string ResultFileName { get; init; }
    /// <summary>Текст для панели «Исходный текст» (для случайного текста; у файла он читается отдельно).</summary>
    public string? SourcePreview { get; init; }
    /// <summary>Сид, которым сгенерирован случайный текст: с ним же можно пересортировать тот же текст другим алгоритмом.</summary>
    public int? RandomSeed { get; init; }
}

/// <summary>Результат одного замера времени (по схеме лабораторной №1: прогрев + несколько прогонов).</summary>
public sealed record WordBenchResult(
    string AlgorithmId,
    string AlgorithmTitle,
    int WordCount,
    string Source,        // описание сгенерированного текста
    int Runs,
    double AvgMs,
    double MinMs,
    double MaxMs,
    long Comparisons,
    long Moves,
    int Passes,
    IReadOnlyList<double> Times);   // время каждого прогона, мс — для истории

/// <summary>
/// Фасад сортировки слов: разбирает текст на слова, запускает выбранный алгоритм,
/// строит трассировку с подсчётом вхождений и пишет результат в файл.
/// </summary>
public sealed class WordSorter
{
    /// <summary>Предел слов для пошаговой визуализации: подготовленные тексты — до 100 слов.</summary>
    public const int MaxTraceWords = 100;

    /// <summary>Предел слов для замеров времени: объём точки задаёт пользователь.</summary>
    public const int MaxBenchWords = 1_000_000;

    /// <summary>Сколько уникальных слов показывать в шагах подсчёта (остальные — одной суммарной строкой).</summary>
    public const int MaxDetailedCounts = 40;

    /// <summary>Сколько прогонов-прогревов делаем перед замерами (как в лабораторной №1).</summary>
    public const int WarmupRuns = 3;

    /// <summary>Сколько строк таблицы подсчёта возвращаем на экран (полный список — в файле результата).</summary>
    public const int MaxCountRows = 300;

    private readonly ITextStore _store;
    private readonly Dictionary<string, IWordSortAlgorithm> _algorithms;

    public WordSorter(ITextStore store, IEnumerable<IWordSortAlgorithm> algorithms)
    {
        _store = store;
        _algorithms = algorithms.ToDictionary(a => a.Id);
    }

    /// <summary>Список доступных алгоритмов (для карточек выбора в интерфейсе).</summary>
    public IReadOnlyList<WordSortMethodInfo> ListMethods() =>
        _algorithms.Values
            .Select(a => new WordSortMethodInfo(a.Id, a.Title, a.Badge, a.ShortInfo, a.PassesLabel, a.MemorySlots))
            .ToList();

    /// <summary>Сортировка подготовленного текста из файла: трассировка + подсчёт вхождений + файл результата.</summary>
    public WordSortRun Run(string fileName, string methodId, bool ascending)
    {
        var words = TextNormalizer.ExtractWords(_store.ReadText(fileName));
        return RunWords(fileName, Path.GetFileNameWithoutExtension(fileName), words, methodId, ascending);
    }

    /// <summary>
    /// Случайный текст: генерируется здесь же и сразу сортируется. Каждый вызов без сидa — новый
    /// случайный текст (состояние между такими прогонами не сохраняется — так и задумано).
    /// Если передан seed, генерируется тот же текст — так один случайный текст можно
    /// пересортировать другим алгоритмом или в другом порядке.
    /// </summary>
    public WordSortRun RunRandom(int wordCount, int? seed, string methodId, bool ascending)
    {
        if (!_algorithms.TryGetValue(methodId, out var algo))
            throw new TableFormatException($"Неизвестный алгоритм сортировки: «{methodId}».");
        int n = Math.Clamp(wordCount, 1, MaxTraceWords);
        int usedSeed = seed ?? Random.Shared.Next();
        string text = TextGenerator.Generate(n, seed: usedSeed);
        var words = TextNormalizer.ExtractWords(text);
        string preview = text.Length > 1200 ? text[..1200] + "\n…" : text;
        return RunWords(
            $"случайный текст ({words.Count} слов)", "random", words, methodId, ascending,
            sourcePreview: preview, randomSeed: usedSeed);
    }

    /// <summary>Общий конвейер: трассировка на всех словах + подсчёт вхождений + файл результата.</summary>
    private WordSortRun RunWords(string displayName, string resultBase,
        IReadOnlyList<string> words, string methodId, bool ascending,
        string? sourcePreview = null, int? randomSeed = null)
    {
        if (!_algorithms.TryGetValue(methodId, out var algo))
            throw new TableFormatException($"Неизвестный алгоритм сортировки: «{methodId}».");
        if (words.Count == 0)
            throw new TableFormatException("В тексте не нашлось ни одного слова — сортировать нечего.");
        if (words.Count > MaxTraceWords)
            throw new TableFormatException(
                $"Для пошаговой визуализации берём до {MaxTraceWords} слов (здесь {words.Count}). " +
                "Большие объёмы сортируйте на панели «Замеры времени».");

        bool descending = !ascending;

        // 1) Демонстрация: сортируем все слова текста с пошаговой трассировкой.
        var demo = MakeWords(words);
        var work = (WordEntry[])demo.Clone(); // алгоритм переставляет копию — исходный порядок нужен для показа
        var builder = new WordSortTraceBuilder(work, active: true);
        algo.Sort(work, descending, builder);

        // 2) Подсчёт: пройдёмся по отсортированному массиву и посчитаем одинаковые слова подряд.
        var demoCounts = CountOccurrences(work);
        builder.CountingPhase(demoCounts, MaxDetailedCounts);
        builder.Done(
            $"Слова стоят по алфавиту, у каждого подсчитано число вхождений: сравнений — {builder.Comparisons}, " +
            $"перестановок — {builder.Moves}, {algo.PassesLabel} — {builder.Passes}. " +
            "Полная таблица подсчёта — на панели «Подсчёт слов», файл результата можно скачать.");
        var trace = builder.Build(work.Select(w => w.Id).ToArray());

        // 3) Полный прогон без трассировки — для таблицы подсчёта и файла результата.
        var full = MakeWords(words);
        algo.SortQuiet(full, descending);
        var counts = CountOccurrences(full);
        string resultFile = _store.WriteCountsResult(resultBase, algo.Id, ascending, counts);

        return new WordSortRun
        {
            FileName = displayName,
            Algorithm = algo,
            Ascending = ascending,
            WordCount = words.Count,
            Words = demo,
            Trace = trace,
            Counts = counts,
            ResultFileName = resultFile,
            SourcePreview = sourcePreview,
            RandomSeed = randomSeed,
        };
    }

    /// <summary>
    /// Замер времени сортировки по схеме лабораторной №1: сначала прогревочные прогоны,
    /// затем runs замеров с Stopwatch; в ответ идут среднее, минимум, максимум и все прогоны.
    /// Замер независим от настроек демонстрации: объём текста задаётся прямо здесь.
    /// Фиксированный сид: один и тот же объём даёт один и тот же текст — замеры воспроизводимы.
    /// </summary>
    public WordBenchResult Benchmark(string algorithmId, int runs, int wordCount)
    {
        if (!_algorithms.TryGetValue(algorithmId, out var algo))
            throw new TableFormatException($"Неизвестный алгоритм сортировки: «{algorithmId}».");
        runs = Math.Clamp(runs, 1, 50);
        int n = Math.Clamp(wordCount, 1, MaxBenchWords);

        var all = TextNormalizer.ExtractWords(TextGenerator.Generate(n, seed: n * 7919));
        if (all.Count == 0)
            throw new TableFormatException("В тексте не нашлось ни одного слова.");

        // Прогрев: разогреваем JIT и кэши, чтобы замеры были честными.
        // На больших объёмах одного прогрева достаточно — иначе он сам занимает заметное время.
        int warmups = all.Count > 200_000 ? 1 : WarmupRuns;
        for (int w = 0; w < warmups; w++)
            algo.SortQuiet(MakeWords(all), descending: false);

        var times = new double[runs];
        WordSortStats stats = new(0, 0, 0);
        for (int r = 0; r < runs; r++)
        {
            var words = MakeWords(all); // свежий массив на каждый прогон — сортировка «на месте»
            var sw = Stopwatch.StartNew();
            stats = algo.SortQuiet(words, descending: false);
            sw.Stop();
            times[r] = sw.Elapsed.TotalMilliseconds;
        }

        return new WordBenchResult(
            algo.Id, algo.Title, all.Count, $"сгенерированный текст на {all.Count} слов", runs,
            AvgMs: times.Average(),
            MinMs: times.Min(),
            MaxMs: times.Max(),
            stats.Comparisons, stats.Moves, stats.Passes,
            times.ToList());
    }

    /// <summary>Разбирает текст на слова (нижний регистр, без знаков препинания).</summary>
    public static List<string> ExtractWords(string text) => TextNormalizer.ExtractWords(text);

    /// <summary>
    /// Главное задание 3: пройтись по отсортированному массиву и для каждого слова посчитать,
    /// сколько раз оно встречается. Одинаковые слова в отсортированном массиве стоят подряд,
    /// поэтому достаточно измерять длину каждой серии одинаковых слов.
    /// </summary>
    public static List<WordCount> CountOccurrences(WordEntry[] sortedWords)
    {
        var result = new List<WordCount>();
        for (int i = 0; i < sortedWords.Length; )
        {
            int j = i + 1;
            while (j < sortedWords.Length && string.CompareOrdinal(sortedWords[j].Text, sortedWords[i].Text) == 0)
                j++;
            result.Add(new WordCount(sortedWords[i].Id, sortedWords[i].Text, j - i));
            i = j;
        }
        return result;
    }

    private static WordEntry[] MakeWords(IReadOnlyList<string> words)
    {
        var result = new WordEntry[words.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = new WordEntry(i, words[i]);
        return result;
    }
}
