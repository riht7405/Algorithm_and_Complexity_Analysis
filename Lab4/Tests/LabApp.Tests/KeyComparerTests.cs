using LabApp.Core;
using Xunit;

namespace LabApp.Tests;

/// <summary>Сравнение по ключевому атрибуту: числа, текст, направление, пустые значения.</summary>
public class KeyComparerTests
{
    private static TableData MakeTable(params string[] keys)
    {
        var rows = keys.Select((k, i) => new RecordRow(i, [k])).ToList();
        var columns = new List<ColumnSpec> { new(0, "Значение", ColumnType.Text) };
        // Тип столбца определим так же, как ридер: если всё парсится как число — Number.
        bool numeric = keys.Where(k => k.Length > 0).All(k => TableFileReader.TryParseNumber(k, out _));
        columns[0] = columns[0] with { Type = numeric ? ColumnType.Number : ColumnType.Text };
        return new TableData("test.csv", ';', true, ["Значение"], columns, rows);
    }

    [Fact]
    public void Numeric_AscendingOrder()
    {
        var table = MakeTable("9", "146", "68.2");
        var cmp = new KeyComparer(table, 0, ascending: true);

        Assert.Equal(-1, Math.Sign(cmp.CompareRaw(2, 1))); // 68.2 < 146
        Assert.Equal(-1, Math.Sign(cmp.CompareRaw(0, 2))); // 9 < 68.2
        Assert.Equal(0, cmp.CompareRaw(1, 1));
    }

    [Fact]
    public void RawCompare_DoesNotDependOnDirection()
    {
        // CompareRaw — «сырое» сравнение по возрастанию; направление сортировки
        // применяют стратегии (ascending ? raw <= 0 : raw >= 0).
        var table = MakeTable("9", "146");
        var asc = new KeyComparer(table, 0, ascending: true);
        var desc = new KeyComparer(table, 0, ascending: false);

        Assert.Equal(Math.Sign(asc.CompareRaw(0, 1)), Math.Sign(desc.CompareRaw(0, 1)));
        Assert.False(desc.Ascending);
    }

    [Fact]
    public void EmptyNumeric_ValuesAlwaysLast()
    {
        var table = MakeTable("", "5", "1");
        var cmp = new KeyComparer(table, 0, ascending: true);

        Assert.Equal(1, Math.Sign(cmp.CompareRaw(0, 1))); // пустое — после 5
        Assert.Equal(1, Math.Sign(cmp.CompareRaw(0, 2))); // пустое — после 1
    }

    [Fact]
    public void Text_RussianAlphabetOrder()
    {
        var table = MakeTable("яблоко", "вишня", "абрикос");
        var cmp = new KeyComparer(table, 0, ascending: true);

        Assert.Equal(-1, Math.Sign(cmp.CompareRaw(2, 1))); // абрикос раньше вишни
        Assert.Equal(-1, Math.Sign(cmp.CompareRaw(1, 0))); // вишня раньше яблока
    }

    [Fact]
    public void Text_IgnoreCase()
    {
        var table = MakeTable("Вишня", "вишня");
        var cmp = new KeyComparer(table, 0, ascending: true);

        Assert.Equal(0, cmp.CompareRaw(0, 1));
    }
}
