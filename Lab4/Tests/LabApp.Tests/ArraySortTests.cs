using LabApp.Core;
using LabApp.Core.ArraySort;
using Xunit;

namespace LabApp.Tests;

/// <summary>Задание 1: все три алгоритма внутренней сортировки действительно сортируют,
/// трассировка согласована с тихим режимом, замеры времени возвращают числа.</summary>
public class ArraySortTests
{
    private static readonly IArraySortAlgorithm[] Algorithms =
    [
        new SelectSort(),
        new InsertSort(),
        new MergeSort(),
    ];

    public static IEnumerable<object[]> AlgorithmIds() =>
        Algorithms.Select(a => new object[] { a.Id });

    private static ArrayEntry[] MakeEntries(int[] values)
    {
        var result = new ArrayEntry[values.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = new ArrayEntry(i, values[i]);
        return result;
    }

    private static int[] SampleValues(int count, int seed)
    {
        // Числа из небольшого диапазона — гарантированные повторы, как в демо-массивах.
        var rnd = new Random(seed);
        var values = new int[count];
        for (int i = 0; i < count; i++)
            values[i] = rnd.Next(ArraySorter.RandomMin, ArraySorter.RandomMax + 1);
        return values;
    }

    private static void AssertSorted(ArrayEntry[] sorted, bool descending)
    {
        var values = sorted.Select(w => w.Value);
        var expected = descending ? values.OrderByDescending(v => v) : values.OrderBy(v => v);
        Assert.Equal(expected, sorted.Select(w => w.Value));
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
            var values = SampleValues(120, seed: 42);

            var asc = MakeEntries(values);
            algo.SortQuiet(asc, descending: false);
            AssertSorted(asc, descending: false);

            var desc = MakeEntries(values);
            algo.SortQuiet(desc, descending: true);
            AssertSorted(desc, descending: true);
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
            Array.Empty<int>(),
            new[] { 7 },
            new[] { 5, 5, 5, 5, 5 },
            new[] { 1, 2, 3, 4, 5, 6, 7 },
            new[] { 7, 6, 5, 4, 3, 2, 1 },
            SampleValues(13, seed: 7),
            new[] { 100, 1, 100, 1, 100 }, // повторы вперемешку
        };
        foreach (var values in cases)
        {
            var entries = MakeEntries(values);
            algo.SortQuiet(entries, descending: false);
            AssertSorted(entries, descending: false);
        }
    }

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void QuietSort_CountersGrow(string algorithmId)
    {
        var algo = Algorithms.First(a => a.Id == algorithmId);
        var stats = algo.SortQuiet(MakeEntries(SampleValues(80, seed: 5)), descending: false);
        Assert.True(stats.Comparisons > 0, "должны быть сравнения");
        Assert.True(stats.Moves > 0, "должны быть перестановки");
        Assert.True(stats.Passes > 0, "должны быть проходы");
    }

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void QuietSort_AlreadySortedArray_IsCheapestForInsertion(string algorithmId)
    {
        // Классика: вставки на отсортированном массиве сравнивают ровно n-1 раз.
        if (algorithmId != "insert") return; // проверка относится к InsertSort
        var algo = Algorithms.First(a => a.Id == algorithmId);
        var values = Enumerable.Range(1, 50).ToArray();
        var stats = algo.SortQuiet(MakeEntries(values), descending: false);
        Assert.Equal(values.Length - 1, stats.Comparisons);
        Assert.Equal(0, stats.Moves); // сдвигать нечего
    }

    [Fact]
    public void MergeSort_IsStableOnEqualValues()
    {
        // При равных числах первым из буфера выходит элемент левой серии — исходный порядок
        // равных сохраняется: их исходные Id в отсортированном массиве возрастают.
        IArraySortAlgorithm merge = new MergeSort();
        var values = new[] { 3, 1, 3, 2, 3, 1, 2, 3 };
        foreach (bool desc in new[] { false, true })
        {
            var entries = MakeEntries(values);
            merge.SortQuiet(entries, desc);
            AssertSorted(entries, desc);
            var byValue = entries.Select((e, i) => (e, i))
                .GroupBy(p => p.e.Value)
                .Select(g => (key: g.Key, ids: g.Select(p => p.e.Id).ToList()));
            foreach (var (_, ids) in byValue)
                Assert.Equal(ids.OrderBy(i => i), ids);
        }
    }

    // ---------- режим трассировки (для визуализации) ----------

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void TraceSort_SortsAndEndsWithDone(string algorithmId)
    {
        var algo = Algorithms.First(a => a.Id == algorithmId);
        var values = SampleValues(60, seed: 11);
        var work = MakeEntries(values);
        var builder = new ArraySortTraceBuilder(work, active: true);
        algo.Sort(work, descending: false, builder);
        var trace = builder.Build(work.Select(w => w.Id).ToArray());

        AssertSorted(work, descending: false);
        Assert.Equal(work.Select(w => w.Id), trace.FinalOrder);
        Assert.NotEmpty(trace.Steps);
        Assert.Contains(trace.Steps, (s) => s.Kind == ArrayStepKind.PhaseStart);
        Assert.Contains(trace.Steps, (s) => s.Kind == ArrayStepKind.Done);
        Assert.Equal(trace.TotalComparisons, trace.Steps[^1].Comparisons);
        Assert.Equal(trace.TotalMoves, trace.Steps[^1].Moves);

        // Снимки массива согласованы: каждый — перестановка исходных Id.
        // Исключение — шаги сдвига вставок: «взятое» число в этот момент в памяти,
        // а сдвинутый элемент на мгновение занимает и старое, и новое место.
        foreach (var step in trace.Steps.Where(s => s.ArrayIds is not null && s.Kind != ArrayStepKind.Shift))
            Assert.Equal(Enumerable.Range(0, values.Length), step.ArrayIds!.OrderBy(i => i));
    }

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void TraceAndQuiet_GiveSameCounters(string algorithmId)
    {
        // Один и тот же вход: тихий режим и трассировка считают одинаковые числа.
        var algo = Algorithms.First(a => a.Id == algorithmId);
        var values = SampleValues(50, seed: 99);

        var quiet = MakeEntries(values);
        var stats = algo.SortQuiet(quiet, descending: false);

        var traced = MakeEntries(values);
        var builder = new ArraySortTraceBuilder(traced, active: true);
        algo.Sort(traced, descending: false, builder);

        Assert.Equal(stats.Comparisons, builder.Comparisons);
        Assert.Equal(stats.Moves, builder.Moves);
        Assert.Equal(stats.Passes, builder.Passes);
    }

    [Theory]
    [MemberData(nameof(AlgorithmIds))]
    public void TraceSort_HandlesDescending(string algorithmId)
    {
        var algo = Algorithms.First(a => a.Id == algorithmId);
        var work = MakeEntries(SampleValues(40, seed: 3));
        var builder = new ArraySortTraceBuilder(work, active: true);
        algo.Sort(work, descending: true, builder);
        AssertSorted(work, descending: true);
    }

    [Fact]
    public void SelectSort_MarksPlacedPositionsProgressively()
    {
        // Выбором: после каждого прохода граничная позиция — окончательное место своего числа.
        var algo = Algorithms.First(a => a.Id == "select");
        var values = SampleValues(15, seed: 4);
        var work = MakeEntries(values);
        var builder = new ArraySortTraceBuilder(work, active: true);
        algo.Sort(work, descending: false, builder);
        var trace = builder.Build(work.Select(w => w.Id).ToArray());

        // Финальный список placed — все числа, у шагов есть снимки placed.
        Assert.Contains(trace.Steps, (s) => s.Placed is { Length: > 0 });
        Assert.Equal(values.Length, trace.Steps[^1].Placed!.Length);
        // В журнале каждая перестановка — обмен двух конкретных позиций (мало обменов).
        Assert.True(trace.TotalMoves <= values.Length, "у выбора — не больше n-1 обменов");
    }

    [Fact]
    public void MergeSort_TraceUsesBufferAndCopyBack()
    {
        var algo = Algorithms.First(a => a.Id == "merge");
        var values = SampleValues(24, seed: 8);
        var work = MakeEntries(values);
        var builder = new ArraySortTraceBuilder(work, active: true);
        algo.Sort(work, descending: false, builder);
        var trace = builder.Build(work.Select(w => w.Id).ToArray());

        var writes = trace.Steps.Where((s) => s.Kind == ArrayStepKind.Write).ToList();
        var copyBacks = trace.Steps.Where((s) => s.Kind == ArrayStepKind.CopyBack).ToList();
        Assert.NotEmpty(writes);
        Assert.NotEmpty(copyBacks);
        // У каждой записи есть снимок буфера, и буфер растёт по одному числу за шаг в рамках прохода.
        Assert.All(writes, (s) => Assert.NotNull(s.BufferIds));
        for (int i = 1; i < writes.Count; i++)
        {
            if (writes[i].Pass == writes[i - 1].Pass)
                Assert.Equal(writes[i - 1].BufferIds!.Length + 1, writes[i].BufferIds!.Length);
        }
        // После CopyBack буфер пуст, а весь массив переписан.
        Assert.All(copyBacks, (s) => Assert.Empty(s.BufferIds!));
        Assert.Equal(values.Length, copyBacks[^1].ArrayIds!.Length);
        // Проходов слияния — ⌈log₂ n⌉: серии удваиваются за проход.
        int expectedPasses = (int)Math.Ceiling(Math.Log2(values.Length));
        Assert.Equal(expectedPasses, trace.TotalPasses);
    }

    [Fact]
    public void InsertSort_TraceShowsShiftsAndPlace()
    {
        var algo = Algorithms.First(a => a.Id == "insert");
        var work = MakeEntries([5, 2, 4, 6, 1, 3]);
        var builder = new ArraySortTraceBuilder(work, active: true);
        algo.Sort(work, descending: false, builder);
        var trace = builder.Build(work.Select(w => w.Id).ToArray());

        Assert.Contains(trace.Steps, (s) => s.Kind == ArrayStepKind.Shift);
        Assert.Contains(trace.Steps, (s) => s.Kind == ArrayStepKind.Place);
        AssertSorted(work, descending: false);
    }

    // ---------- фасад ArraySorter ----------

    [Fact]
    public void Sorter_Run_TracesSelectedAlgorithmAndKeepsOriginal()
    {
        var sorter = new ArraySorter(Algorithms);
        int[] values = [5, 23, 8, 42, 8, 15];
        var run = sorter.Run(values, "merge", ascending: true);

        Assert.Equal("merge", run.Algorithm.Id);
        Assert.Equal(values.Select((v, i) => (v, i)), run.Elements.Select((e) => (e.Value, e.Id)));
        Assert.True(run.Trace.Steps.Count > 0);
        Assert.Equal(run.Elements.Length, run.Trace.FinalOrder.Length);
    }

    [Fact]
    public void Sorter_Run_RejectsEmptyAndTooBigArrays()
    {
        var sorter = new ArraySorter(Algorithms);
        Assert.Throws<TableFormatException>(() => sorter.Run([], "select", ascending: true));
        Assert.Throws<TableFormatException>(() =>
            sorter.Run(Enumerable.Range(0, ArraySorter.MaxTraceElements + 1).ToArray(), "select", ascending: true));
        Assert.Throws<TableFormatException>(() => sorter.Run([1], "no-such-algo", ascending: true));
    }

    [Fact]
    public void Sorter_ParseValues_SplitsOnSpacesCommasAndNewLines()
    {
        Assert.Equal(new[] { 5, 23, 8, 42 }, ArraySorter.ParseValues("5, 23; 8\n42"));
        Assert.Equal(new[] { 7 }, ArraySorter.ParseValues("7"));
        Assert.Equal([], ArraySorter.ParseValues("   "));
        Assert.Throws<TableFormatException>(() => ArraySorter.ParseValues("5, oops"));
    }

    [Fact]
    public void Sorter_Generate_IsDeterministicAndVariesBySeed()
    {
        int[] a = ArraySorter.Generate(500, seed: 10);
        int[] b = ArraySorter.Generate(500, seed: 10);
        int[] c = ArraySorter.Generate(500, seed: 11);
        Assert.Equal(a, b);    // фиксированный сид → тот же массив
        Assert.NotEqual(a, c); // другой сид → другой массив
        Assert.All(a, (v) => Assert.InRange(v, ArraySorter.RandomMin, ArraySorter.RandomMax));
    }

    [Fact]
    public void Sorter_RandomArray_SameSeedGivesSameArray()
    {
        var sorter = new ArraySorter(Algorithms);
        var (values1, seed1) = sorter.RandomArray(20, seed: 77);
        var (values2, _) = sorter.RandomArray(20, seed: seed1);
        Assert.Equal(values1, values2);
        Assert.Equal(20, values1.Length);
    }

    // ---------- замеры времени ----------

    [Fact]
    public void Sorter_Benchmark_ReturnsTimeAndCounters()
    {
        var sorter = new ArraySorter(Algorithms);
        var result = sorter.Benchmark("select", runs: 3, elementCount: 300);
        Assert.Equal(300, result.ElementCount);
        Assert.Equal(3, result.Runs);
        Assert.True(result.AvgMs > 0);
        Assert.True(result.MinMs > 0 && result.MinMs <= result.AvgMs);
        Assert.True(result.MaxMs >= result.AvgMs);
        Assert.True(result.Comparisons > 0);
        Assert.Equal(3, result.Times.Count); // время каждого прогона — для истории
        Assert.All(result.Times, (t) => Assert.True(t > 0));
    }

    [Fact]
    public void Sorter_Benchmark_UnknownAlgorithm_Throws()
    {
        var sorter = new ArraySorter(Algorithms);
        Assert.Throws<TableFormatException>(() =>
            sorter.Benchmark("no-such-algo", 1, elementCount: 20));
    }
}
