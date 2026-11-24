using System.Diagnostics;
using LabApp.Core.Texts;

namespace LabApp.Core.WordSort;

/// <summary>Одна запись подсчёта: слово и сколько раз оно встретилось в отсортированном массиве.</summary>
public sealed record WordCount(int WordId, string Word, int Count);

/// <summary>Итог одного запуска сортировки слов (задание 3).</summary>
public sealed class WordSortRun
{
    public required string FileName { get; init; }
    public required IWordSortAlgorithm Algorithm { get; init; }
    public required bool Ascending { get; init; }
    public required int LimitWords { get; init; }          // сколько слов участвовало в демонстрации
    public required int TotalWords { get; init; }          // всего слов в тексте
    public required int UniqueWords { get; init; }         // уникальных слов во всём тексте
    public required WordEntry[] DemoWords { get; init; }   // исходный (несортированный) список показанных слов
    public required WordSortTrace Trace { get; init; }
    public required IReadOnlyList<WordCount> FullCounts { get; init; } // подсчёт по всему тексту
    public required string ResultFileName { get; init; }
}

/// <summary>Результат одного замера времени (по схеме лабораторной №1: прогрев + несколько прогонов).</summary>
public sealed record WordBenchResult(
    string AlgorithmId,
    string AlgorithmTitle,
    int WordCount,
    string Source,        // имя файла или описание сгенерированного текста
    int Runs,
    double AvgMs,
    double MinMs,
    double MaxMs,
    long Comparisons,
    long Moves,
    int Passes);

/// <summary>
/// Фасад сортировки слов: разбирает текст на слова, запускает выбранный алгоритм,
/// строит трассировку с подсчётом вхождений и пишет результат в файл.
/// </summary>
public sealed class WordSorter
{
    /// <summary>Предел слов для пошаговой визуализации: длинные тексты демонстрируем на первых N словах.</summary>
    public const int MaxTraceWords = 100;
    public const int MinTraceWords = 10;

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

    /// <summary>Полный запуск задания 3: сортировка с трассировкой + подсчёт вхождений + файл результата.</summary>
    public WordSortRun Run(string fileName, string methodId, bool ascending, int limitWords)
    {
        if (!_algorithms.TryGetValue(methodId, out var algo))
            throw new TableFormatException($"Неизвестный алгоритм сортировки: «{methodId}».");

        var all = TextNormalizer.ExtractWords(_store.ReadText(fileName));
        if (all.Count == 0)
            throw new TableFormatException("В файле не нашлось ни одного слова — сортировать нечего.");

        bool descending = !ascending;
        int limit = Math.Clamp(limitWords, MinTraceWords, MaxTraceWords);

        // 1) Демонстрация: сортируем первые limit слов с пошаговой трассировкой.
        var demo = all.Take(limit).Select((t, i) => new WordEntry(i, t)).ToArray();
        var work = (WordEntry[])demo.Clone(); // алгоритм переставляет копию — исходный порядок нужен для показа
        var builder = new WordSortTraceBuilder(work, active: true);
        algo.Sort(work, descending, builder);

        // 2) Подсчёт: пройдёмся по отсортированному массиву и посчитаем одинаковые слова подряд.
        var demoCounts = CountOccurrences(work);
        builder.CountingPhase(demoCounts, MaxDetailedCounts);
        builder.Done(
            $"Слова стоят по алфавиту, у каждого подсчитано число вхождений: сравнений — {builder.Comparisons}, " +
            $"перестановок — {builder.Moves}, {algo.PassesLabel} — {builder.Passes}. " +
            "Полная таблица подсчёта по всему тексту — на панели «Подсчёт слов», файл результата можно скачать.");
        var trace = builder.Build(work.Select(w => w.Id).ToArray());

        // 3) Полный прогон по всему тексту без трассировки — для таблицы подсчёта и файла результата.
        var full = MakeWords(all);
        algo.SortQuiet(full, descending);
        var fullCounts = CountOccurrences(full);
        string resultFile = _store.WriteCountsResult(fileName, algo.Id, ascending, fullCounts);

        return new WordSortRun
        {
            FileName = fileName,
            Algorithm = algo,
            Ascending = ascending,
            LimitWords = limit,
            TotalWords = all.Count,
            UniqueWords = fullCounts.Count,
            DemoWords = demo,
            Trace = trace,
            FullCounts = fullCounts,
            ResultFileName = resultFile,
        };
    }

    /// <summary>
    /// Замер времени сортировки по схеме лабораторной №1: сначала прогревочные прогоны,
    /// затем runs замеров с Stopwatch; в таблицу идут среднее, минимум и максимум.
    /// Текст берём из файла или генерируем указанной длины (сид фиксированный — текст воспроизводим).
    /// </summary>
    public WordBenchResult Benchmark(string algorithmId, int runs, int? wordCount, string? fileName)
    {
        if (!_algorithms.TryGetValue(algorithmId, out var algo))
            throw new TableFormatException($"Неизвестный алгоритм сортировки: «{algorithmId}».");
        runs = Math.Clamp(runs, 1, 20);

        List<string> all;
        string source;
        if (wordCount is not null)
        {
            int n = Math.Clamp(wordCount.Value, MinTraceWords, 200_000);
            // Фиксированный сид: один и тот же объём даёт один и тот же текст — замеры воспроизводимы.
            all = TextNormalizer.ExtractWords(TextGenerator.Generate(n, seed: n * 7919));
            source = $"сгенерированный текст на {all.Count} слов";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new TableFormatException("Укажите объём текста (wordCount) или имя файла (fileName).");
            all = TextNormalizer.ExtractWords(_store.ReadText(fileName));
            source = fileName;
        }
        if (all.Count == 0)
            throw new TableFormatException("В тексте не нашлось ни одного слова.");

        // Прогрев: разогреваем JIT и кэши, чтобы замеры были честными.
        for (int w = 0; w < WarmupRuns; w++)
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
            algo.Id, algo.Title, all.Count, source, runs,
            AvgMs: times.Average(),
            MinMs: times.Min(),
            MaxMs: times.Max(),
            stats.Comparisons, stats.Moves, stats.Passes);
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
