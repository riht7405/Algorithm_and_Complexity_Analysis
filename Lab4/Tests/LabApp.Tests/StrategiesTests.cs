using LabApp.Core;
using LabApp.Core.ExternalSort;
using Xunit;

namespace LabApp.Tests;

/// <summary>Главное: все стратегии действительно сортируют таблицу и пишут понятные шаги.</summary>
public class StrategiesTests
{
    private static TableData MakeTable(string[] keys, string[] labels)
    {
        var rows = keys.Select((k, i) => new RecordRow(i, [labels[i], k])).ToList();
        // Тип столбца определяем так же, как TableFileReader: всё парсится — число.
        bool numeric = keys.Where(k => k.Length > 0).All(k => TableFileReader.TryParseNumber(k, out _));
        var columns = new List<ColumnSpec>
        {
            new(0, "Название", ColumnType.Text),
            new(1, "Значение", numeric ? ColumnType.Number : ColumnType.Text),
        };
        return new TableData("test.csv", ';', true, ["Название", "Значение"], columns, rows);
    }

    private static readonly IExternalSortStrategy[] Strategies =
    [
        new DirectMergeStrategy(),
        new NaturalMergeStrategy(),
        new MultiwayMergeStrategy(),
    ];

    public static IEnumerable<object[]> StrategyIds() =>
        Strategies.Select(s => new object[] { s.Id });

    private static void AssertSorted(SortTrace trace, TableData table, int keyColumn, bool ascending)
    {
        var cmp = new KeyComparer(table, keyColumn, ascending: true); // «сырое» сравнение
        var order = trace.FinalOrder;
        Assert.Equal(table.RowCount, order.Count);
        Assert.Equal(Enumerable.Range(0, table.RowCount).OrderBy(i => i), order.OrderBy(i => i));
        for (int i = 1; i < order.Count; i++)
        {
            int raw = cmp.CompareRaw(order[i - 1], order[i]);
            Assert.True(ascending ? raw <= 0 : raw >= 0,
                $"нарушен порядок: позиции {i - 1} и {i}");
        }
    }

    [Theory]
    [MemberData(nameof(StrategyIds))]
    public void Sorts_Numbers_Ascending(string strategyId)
    {
        var table = MakeTable(
            ["9", "146", "68.2", "1", "40.1", "216.4", "26.6", "84.5"],
            ["a", "b", "c", "d", "e", "f", "g", "h"]);
        var strategy = Strategies.First(s => s.Id == strategyId);

        var trace = strategy.Execute(table, 1, ascending: true);

        AssertSorted(trace, table, 1, ascending: true);
        Assert.Equal<string>(["1", "9", "26.6", "40.1", "68.2", "84.5", "146", "216.4"],
            trace.FinalOrder.Select(id => table.Rows[id].Cells[1]).ToArray());
    }

    [Theory]
    [MemberData(nameof(StrategyIds))]
    public void Sorts_Numbers_Descending(string strategyId)
    {
        var table = MakeTable(
            ["9", "146", "68.2", "1", "40.1", "216.4", "26.6", "84.5"],
            ["a", "b", "c", "d", "e", "f", "g", "h"]);
        var strategy = Strategies.First(s => s.Id == strategyId);

        var trace = strategy.Execute(table, 1, ascending: false);

        AssertSorted(trace, table, 1, ascending: false);
        Assert.Equal(216.4, double.Parse(table.Rows[trace.FinalOrder[0]].Cells[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [MemberData(nameof(StrategyIds))]
    public void Sorts_RussianText(string strategyId)
    {
        string[] words = ["яблоко", "вишня", "абрикос", "банан", "груша", "дыня", "ежевика", "лимон"];
        var table = MakeTable(words, words);
        var strategy = Strategies.First(s => s.Id == strategyId);

        var trace = strategy.Execute(table, 1, ascending: true);

        var sorted = trace.FinalOrder.Select(id => table.Rows[id].Cells[1]).ToArray();
        Assert.Equal(["абрикос", "банан", "вишня", "груша", "дыня", "ежевика", "лимон", "яблоко"], sorted);
    }

    [Theory]
    [MemberData(nameof(StrategyIds))]
    public void EqualKeys_StayInOriginalOrder(string strategyId)
    {
        // Все ключи одинаковые — записи должны остаться в исходном порядке (стабильность).
        var table = MakeTable(
            ["5", "5", "5", "5", "5", "5"],
            ["a", "b", "c", "d", "e", "f"]);
        var strategy = Strategies.First(s => s.Id == strategyId);

        var trace = strategy.Execute(table, 1, ascending: true);

        Assert.Equal(Enumerable.Range(0, 6), trace.FinalOrder);
    }

    [Theory]
    [MemberData(nameof(StrategyIds))]
    public void Logs_ComparisonsAndMoves(string strategyId)
    {
        var table = MakeTable(
            ["9", "146", "68.2", "1", "40.1"],
            ["a", "b", "c", "d", "e"]);
        var strategy = Strategies.First(s => s.Id == strategyId);

        var trace = strategy.Execute(table, 1, ascending: true);

        Assert.True(trace.TotalComparisons > 0, "нет ни одного сравнения");
        Assert.True(trace.TotalMoves > 0, "нет ни одной перестановки");
        Assert.True(trace.Steps.Count > 5, "слишком мало шагов визуализации");
        Assert.Equal(StepKind.Done, trace.Steps[^1].Kind); // последний шаг — финиш
        Assert.Contains("сравнений", trace.Steps[^1].Desc);
        // Все шаги со сравнением и перестановкой несут строку журнала.
        Assert.All(trace.Steps.Where(s => s.Kind is StepKind.Compare or StepKind.Move or StepKind.Flush),
            s => Assert.False(string.IsNullOrEmpty(s.LogLine)));
    }

    [Fact]
    public void NaturalMerge_AlreadySorted_NeedsOnePass()
    {
        var table = MakeTable(["1", "2", "3", "4"], ["a", "b", "c", "d"]);

        var trace = new NaturalMergeStrategy().Execute(table, 1, ascending: true);

        Assert.Equal(0, trace.TotalPasses);
        Assert.Equal([0, 1, 2, 3], trace.FinalOrder);
    }

    [Fact]
    public void NaturalMerge_PartiallySorted_FewerPassesThanDirect()
    {
        // Половина файла уже упорядочена — естественное слияние должно справиться быстрее.
        var table = MakeTable(["5", "6", "7", "1", "2", "3", "4", "8"], ["a", "b", "c", "d", "e", "f", "g", "h"]);

        var natural = new NaturalMergeStrategy().Execute(table, 1, ascending: true);
        var direct = new DirectMergeStrategy().Execute(table, 1, ascending: true);

        AssertSorted(natural, table, 1, true);
        AssertSorted(direct, table, 1, true);
        Assert.True(natural.TotalComparisons < direct.TotalComparisons,
            $"естественное слияние не дешевле прямого: {natural.TotalComparisons} vs {direct.TotalComparisons}");
    }

    [Fact]
    public void DirectMerge_MemoryNote_MentionsTwoValues()
    {
        var table = MakeTable(["3", "1", "2"], ["a", "b", "c"]);

        var trace = new DirectMergeStrategy().Execute(table, 1, ascending: true);

        Assert.Contains(trace.Steps, s => s.MemoryNote.Contains("ровно 2 значения"));
        Assert.All(trace.Steps, s => Assert.True(s.MemoryIds.Length <= 2, "в памяти больше 2 значений"));
    }

    [Fact]
    public void MultiwayMerge_UsesFourTapes()
    {
        var table = MakeTable(
            ["16", "3", "9", "1", "7", "12", "5", "2", "11", "8", "14", "4", "10", "6", "13", "15"],
            Enumerable.Range(0, 16).Select(i => $"r{i}").ToArray());

        var trace = new MultiwayMergeStrategy().Execute(table, 1, ascending: true);

        AssertSorted(trace, table, 1, true);
        Assert.Contains(trace.Steps, s => s.MemoryIds.Length == 4); // 4 значения в памяти
        Assert.All(trace.Steps, s => Assert.True(s.MemoryIds.Length <= 4));
        Assert.True(trace.TotalPasses >= 3); // распределение + 2 слияния минимум
    }

    [Fact]
    public void SingleRow_AlreadySorted()
    {
        var table = MakeTable(["1"], ["a"]);

        foreach (var strategy in Strategies)
        {
            var trace = strategy.Execute(table, 1, ascending: true);
            Assert.Equal([0], trace.FinalOrder);
        }
    }
}
