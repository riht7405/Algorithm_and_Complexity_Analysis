using LabApp.Core;
using LabApp.Core.Texts;
using LabApp.Core.WordSort;
using Xunit;

namespace LabApp.Tests;

/// <summary>Задание 3: оба алгоритма сортировки слов действительно сортируют, подсчёт вхождений сходится,
/// генератор текстов выдаёт нужную длину, замеры времени возвращают числа.</summary>
public class WordSortTests
{
    private static readonly IWordSortAlgorithm[] Algorithms =
    [
        new QuickSortImproved(),
        new AbcSort(),
    ];

    public static IEnumerable<object[]> AlgorithmIds() =>
        Algorithms.Select(a => new object[] { a.Id });

    private static WordEntry[] MakeWords(string[] texts)
    {
        var words = new WordEntry[texts.Length];
        for (int i = 0; i < texts.Length; i++)
            words[i] = new WordEntry(i, texts[i]);
        return words;
    }

    private static string[] SampleWords(int count, int seed)
    {
        // Немного «настоящего» текста: частые повторы + разнообразие.
        var rnd = new Random(seed);
        var pool = new[] { "the", "cat", "sat", "on", "mat", "a", "dog", "ran", "big", "small",
            "apple", "pie", "sun", "moon", "car", "bus", "word", "sort", "test", "lab" };
        var words = new string[count];
        for (int i = 0; i < count; i++)
            words[i] = pool[rnd.Next(pool.Length)];
        return words;
    }

    private static void AssertSortedByOrdinal(WordEntry[] sorted, bool descending)
    {
        var expected = sorted.Select(w => w.Text).OrderBy(w => w, StringComparer.Ordinal);
        if (descending) expected = expected.OrderByDescending(w => w, StringComparer.Ordinal);
        Assert.Equal(expected, sorted.Select(w => w.Text));
        // Id не потерялись: это перестановка исходных номеров 0..n-1.
        Assert.Equal(Enumerable.Range(0, sorted.Length), sorted.Select(w => w.Id).OrderBy(i => i));
    }

    // ---------- тихий режим (для замеров времени) ----------

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void QuietSort_SortsAscendingAndDescending(string algorithmId)
    {
        foreach (var algo in Algorithms.Where(a => a.Id == algorithmId))
        {
            var words = MakeWords(SampleWords(120, seed: 42));

            var asc = (WordEntry[])words.Clone();
            algo.SortQuiet(asc, descending: false);
            AssertSortedByOrdinal(asc, descending: false);

            var desc = (WordEntry[])words.Clone();
            algo.SortQuiet(desc, descending: true);
            AssertSortedByOrdinal(desc, descending: true);
        }
    }

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void QuietSort_HandlesEdgeCases(string algorithmId)
    {
        var algo = Algorithms.First(a => a.Id == algorithmId);
        // пустой, один, все одинаковые, уже отсортированные, обратно отсортированные
        var cases = new[]
        {
            Array.Empty<string>(),
            new[] { "one" },
            new[] { "same", "same", "same", "same" },
            new[] { "a", "b", "c", "d", "e" },
            new[] { "e", "d", "c", "b", "a" },
            SampleWords(13, seed: 7),   // граница порога вставок QuickSort
            new[] { "mother-in-law", "mother", "moth", "mothership" }, // дефисы и префиксы
        };
        foreach (var texts in cases)
        {
            var words = MakeWords(texts);
            algo.SortQuiet(words, descending: false);
            AssertSortedByOrdinal(words, descending: false);
        }
    }

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void QuietSort_CountersGrow(string algorithmId)
    {
        var algo = Algorithms.First(a => a.Id == algorithmId);
        var stats = algo.SortQuiet(MakeWords(SampleWords(80, seed: 5)), descending: false);
        Assert.True(stats.Comparisons > 0, "должны быть сравнения");
        Assert.True(stats.Moves > 0, "должны быть перестановки");
        Assert.True(stats.Passes > 0, "должны быть проходы");
    }

    // ---------- режим трассировки (для визуализации) ----------

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void TraceSort_SortsAndEndsWithDone(string algorithmId)
    {
        var algo = Algorithms.First(a => a.Id == algorithmId);
        var texts = SampleWords(60, seed: 11);
        var work = MakeWords(texts);
        var builder = new WordSortTraceBuilder(work, active: true);
        algo.Sort(work, descending: false, builder);
        var trace = builder.Build(work.Select(w => w.Id).ToArray());

        AssertSortedByOrdinal(work, descending: false);
        Assert.Equal(work.Select(w => w.Id), trace.FinalOrder);
        Assert.NotEmpty(trace.Steps);
        // Шаг Done и подсчёт добавляет фасад WordSorter.Run — здесь трассировка только сортировки.
        Assert.Contains(trace.Steps, (s) => s.Kind == WordStepKind.PhaseStart);
        Assert.Equal(trace.TotalComparisons, trace.Steps[^1].Comparisons);
        Assert.Equal(trace.TotalMoves, trace.Steps[^1].Moves);

        // Снимки массива согласованы: каждый — перестановка исходных Id.
        // Исключение — шаги сдвига вставок: «взятое» слово в этот момент в памяти,
        // и сдвинутый элемент на мгновение занимает и старое, и новое место.
        foreach (var step in trace.Steps.Where(s => s.ArrayIds is not null && s.Kind != WordStepKind.Shift))
            Assert.Equal(Enumerable.Range(0, texts.Length), step.ArrayIds!.OrderBy(i => i));
    }

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void TraceAndQuiet_GiveSameCounters(string algorithmId)
    {
        // Один и тот же вход: тихий режим и трассировка считают одинаковые числа.
        var algo = Algorithms.First(a => a.Id == algorithmId);
        var texts = SampleWords(50, seed: 99);

        var quiet = MakeWords(texts);
        var stats = algo.SortQuiet(quiet, descending: false);

        var traced = MakeWords(texts);
        var builder = new WordSortTraceBuilder(traced, active: true);
        algo.Sort(traced, descending: false, builder);

        Assert.Equal(stats.Comparisons, builder.Comparisons);
        Assert.Equal(stats.Moves, builder.Moves);
        Assert.Equal(stats.Passes, builder.Passes);
    }

    [Fact]
    public void QuickSort_DoesNotOverflowStackOnLargeSortedInput()
    {
        // Классическая ловушка QuickSort: уже отсортированный большой массив.
        // Улучшения (медиана из трёх + вставки + цикл по большему куску) должны спасать.
        IWordSortAlgorithm quick = new QuickSortImproved();
        var words = MakeWords(Enumerable.Range(0, 20_000).Select(i => $"w{i:D5}").ToArray());
        quick.SortQuiet(words, descending: false);
        AssertSortedByOrdinal(words, descending: false);
    }

    [Fact]
    public void AbcSort_LongWords_DontRecurseForever()
    {
        IWordSortAlgorithm abc = new AbcSort();
        var texts = Enumerable.Range(0, 30).Select(i => "abc" + new string('x', i)).ToArray();
        var words = MakeWords(texts);
        abc.SortQuiet(words, descending: false);
        AssertSortedByOrdinal(words, descending: false);
    }

    // ---------- подсчёт вхождений ----------

    [Fact]
    public void CountOccurrences_WalksSortedArrayAndCounts()
    {
        var sorted = MakeWords(["a", "a", "b", "b", "b", "c"]);
        var counts = WordSorter.CountOccurrences(sorted);
        Assert.Equal(
        [
            new WordCount(sorted[0].Id, "a", 2),
            new WordCount(sorted[2].Id, "b", 3),
            new WordCount(sorted[5].Id, "c", 1),
        ], counts);
    }

    [Fact]
    public void CountOccurrences_EmptyArray()
    {
        Assert.Empty(WordSorter.CountOccurrences([]));
    }

    // ---------- разбор текста ----------

    [Fact]
    public void TextNormalizer_StripsPunctuationAndLowercases()
    {
        var words = TextNormalizer.ExtractWords("Hello, World! The CAT-sat; it's (fine) 2026.");
        Assert.Equal(["hello", "world", "the", "cat-sat", "its", "fine", "2026"], words);
    }

    [Fact]
    public void TextNormalizer_TrimsTrailingHyphen()
    {
        Assert.Equal("word", TextNormalizer.CleanWord("Word--"));
        Assert.Equal("mother-in-law", TextNormalizer.CleanWord("Mother-in-Law,"));
        Assert.Equal("", TextNormalizer.CleanWord("..."));
    }

    // ---------- генератор текстов ----------

    [Fact]
    public void TextGenerator_GeneratesRequestedWordCount()
    {
        foreach (int length in TextGenerator.DefaultLengths.Append(17))
        {
            var words = TextNormalizer.ExtractWords(TextGenerator.Generate(length, seed: 1));
            Assert.Equal(length, words.Count);
        }
    }

    [Fact]
    public void TextGenerator_IsDeterministicAndVariesBySeed()
    {
        string a = TextGenerator.Generate(200, seed: 10);
        string b = TextGenerator.Generate(200, seed: 10);
        string c = TextGenerator.Generate(200, seed: 11);
        Assert.Equal(a, b);      // фиксированный сид → тот же текст
        Assert.NotEqual(a, c);   // другой сид → другой текст
    }

    [Fact]
    public void TextGenerator_HasRepeatsForCountingDemo()
    {
        // Распределение Ципфа даёт повторяющиеся слова — иначе подсчёт неинтересен.
        var words = TextNormalizer.ExtractWords(TextGenerator.Generate(500, seed: 3));
        Assert.True(words.Count > words.Distinct().Count(), "в тексте должны быть повторы");
    }

    // ---------- хранилище текстов ----------

    [Fact]
    public void TextStore_SaveReadListAndResult()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));

            string name = store.SaveGenerated(10, "one two three four five six seven eight nine ten");
            Assert.Equal("text_10.txt", name);
            Assert.Equal("one two three four five six seven eight nine ten", store.ReadText(name));
            Assert.Single(store.ListTexts());

            var counts = new List<WordCount>
            {
                new(0, "one", 1), new(1, "three", 2),
            };
            string result = store.WriteCountsResult(name, "abc", ascending: true, counts);
            Assert.StartsWith("words_text_10_abc_asc_", result);
            Assert.True(File.Exists(store.ResultPath(result)));

            // Защита от обхода каталогов.
            Assert.Throws<TableFormatException>(() => store.ReadText("../secret.txt"));
            Assert.Throws<FileNotFoundException>(() => store.ReadText("no-such-file.txt"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // ---------- фасад WordSorter ----------

    [Fact]
    public void WordSorter_Benchmark_ReturnsTimeAndCounters()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            var sorter = new WordSorter(store, Algorithms);

            var result = sorter.Benchmark("abc", runs: 3, wordCount: 60);
            Assert.Equal(60, result.WordCount);
            Assert.Equal(3, result.Runs);
            Assert.True(result.AvgMs > 0);
            Assert.True(result.MinMs > 0 && result.MinMs <= result.AvgMs);
            Assert.True(result.MaxMs >= result.AvgMs);
            Assert.True(result.Comparisons > 0);
            Assert.Equal(3, result.Times.Count); // время каждого прогона — для истории
            Assert.All(result.Times, (t) => Assert.True(t > 0));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WordSorter_Benchmark_UnknownAlgorithm_Throws()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            var sorter = new WordSorter(store, Algorithms);
            Assert.Throws<TableFormatException>(() =>
                sorter.Benchmark("no-such-algo", 1, wordCount: 20));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void WordSorter_Run_FullPipeline_CountsMatchFile(string algorithmId)
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            store.SaveGenerated(40, TextGenerator.Generate(40, seed: 8));
            var sorter = new WordSorter(store, Algorithms);

            var run = sorter.Run("text_40.txt", algorithmId, ascending: true);

            // Сортируются ВСЕ слова файла, трассировка заканчивается Done.
            Assert.Equal(40, run.WordCount);
            Assert.Equal(40, run.Words.Length);
            Assert.Null(run.SourcePreview); // у файла исходный текст читается превью-запросом
            Assert.Null(run.RandomSeed);
            Assert.Equal(WordStepKind.Done, run.Trace.Steps[^1].Kind);

            // Подсчёт по всему тексту: сумма вхождений = числу слов, слова в алфавитном порядке.
            Assert.Equal(40, run.Counts.Sum(c => c.Count));
            var words = run.Counts.Select(c => c.Word).ToArray();
            Assert.Equal(words.OrderBy(w => w, StringComparer.Ordinal), words);

            // Файл результата записан и содержит заголовок и все уникальные слова.
            string path = store.ResultPath(run.ResultFileName);
            var lines = File.ReadAllLines(path);
            Assert.Equal("слово;вхождений", lines[0]);
            Assert.Equal(run.Counts.Count, lines.Length - 1);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WordSorter_Run_EmptyText_Throws()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            store.SaveGenerated(0, "...");
            var sorter = new WordSorter(store, Algorithms);
            Assert.Throws<TableFormatException>(() =>
                sorter.Run("text_0.txt", "quick-improved", ascending: true));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WordSorter_Run_TooManyWords_ThrowsWithHint()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            store.SaveGenerated(150, TextGenerator.Generate(150, seed: 2));
            var sorter = new WordSorter(store, Algorithms);
            var ex = Assert.Throws<TableFormatException>(() =>
                sorter.Run("text_150.txt", "abc", ascending: true));
            Assert.Contains("Замеры времени", ex.Message);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WordSorter_RunRandom_EachCallIsANewRandomText()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            var sorter = new WordSorter(store, Algorithms);

            var first = sorter.RunRandom(30, seed: null, "abc", ascending: true);
            var second = sorter.RunRandom(30, seed: null, "abc", ascending: true);

            // Каждый вызов — новый случайный текст (без сидa случайность берётся из энтропии).
            Assert.NotEqual(
                first.Words.Select(w => w.Text),
                second.Words.Select(w => w.Text));
            Assert.NotNull(first.SourcePreview);
            Assert.NotNull(first.RandomSeed);
            Assert.Equal(30, first.WordCount);
            Assert.Equal(WordStepKind.Done, first.Trace.Steps[^1].Kind);
            Assert.StartsWith("words_random_", first.ResultFileName);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WordSorter_RunRandom_SameSeed_ReproducesSameText()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            var sorter = new WordSorter(store, Algorithms);

            var first = sorter.RunRandom(25, seed: 12345, "quick-improved", ascending: true);
            var second = sorter.RunRandom(25, seed: 12345, "abc", ascending: true);

            // Тот же сид → тот же текст: один случайный текст можно пересортировать другим алгоритмом.
            Assert.Equal(
                first.Words.Select(w => w.Text),
                second.Words.Select(w => w.Text));
            Assert.Equal(12345, first.RandomSeed);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WordSorter_RunRandom_ClampsToDemoLimit()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            var sorter = new WordSorter(store, Algorithms);
            var run = sorter.RunRandom(5000, seed: 7, "abc", ascending: true);
            Assert.Equal(WordSorter.MaxTraceWords, run.WordCount); // 5000 → зажимается до 100
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // ---------- каталог готовых текстов ----------

    [Fact]
    public void TextCatalog_ShowsPreparedTextsAndHidesOversizedGenerated()
    {
        Assert.True(TextCatalog.IsVisibleInDemo("text_5.txt", 100));
        Assert.True(TextCatalog.IsVisibleInDemo("text_100.txt", 100));
        Assert.False(TextCatalog.IsVisibleInDemo("text_500.txt", 100));   // устаревшие большие тексты
        Assert.False(TextCatalog.IsVisibleInDemo("text_5000.txt", 100));
        Assert.True(TextCatalog.IsVisibleInDemo("my_story.txt", 100));    // свой файл пользователя
    }

    [Fact]
    public void TextStore_EnsureDefaults_CreatesWholePreparedSet()
    {
        string root = Path.Combine(Path.GetTempPath(), "lab4tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemTextStore(
                Path.Combine(root, "texts"), Path.Combine(root, "results"));
            store.EnsureDefaults();
            var names = store.ListTexts().Select((t) => t.FileName).ToHashSet();
            foreach (int length in TextGenerator.DefaultLengths)
                Assert.Contains(TextGenerator.FileNameFor(length), names);

            // Повторный вызов ничего не пересоздаёт (файлы на месте, набор тот же).
            store.EnsureDefaults();
            Assert.Equal(TextGenerator.DefaultLengths.Length, store.ListTexts().Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
