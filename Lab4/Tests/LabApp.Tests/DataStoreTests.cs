using LabApp.Core;
using Xunit;

namespace LabApp.Tests;

/// <summary>Результирующий файл: правильное имя, заголовок, порядок строк, CSV-экранирование.</summary>
public class DataStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lab4-store-" + Guid.NewGuid().ToString("N"));

    public DataStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static TableData MakeTable(bool hasHeader = true)
    {
        var rows = new List<RecordRow>
        {
            new(0, ["Груша", "40.1"]),
            new(1, ["Яблоко, лесное", "9"]),
            new(2, ["Абрикос", "146.2"]),
        };
        var columns = new List<ColumnSpec> { new(0, "Название", ColumnType.Text), new(1, "Масса", ColumnType.Number) };
        return new TableData("fruits.csv", ';', hasHeader, ["Название", "Масса"], columns, rows);
    }

    [Fact]
    public void WriteResult_CreatesFile_WithHeaderAndSortedRows()
    {
        var store = new FileSystemDataStore(_dir);
        // Сортирует ExternalSorter, а WriteResult лишь записывает готовый порядок.
        var table = MakeTable().Reordered([1, 0, 2]);

        string fileName = store.WriteResult("fruits.csv", table, "direct", "Масса", ascending: true);

        Assert.StartsWith("fruits_direct_Масса_asc_", fileName);
        Assert.EndsWith(".csv", fileName);

        string path = Path.Combine(_dir, "results", fileName);
        Assert.True(File.Exists(path));

        string[] lines = File.ReadAllLines(path);
        Assert.Equal(4, lines.Length); // заголовок + 3 записи
        Assert.Equal("Название;Масса", lines[0]);
        Assert.Equal("Яблоко, лесное;9", lines[1]); // кавычки не нужны: разделителя внутри нет, запятая не «;»
        Assert.Equal("Груша;40.1", lines[2]);
        Assert.Equal("Абрикос;146.2", lines[3]);
    }

    [Fact]
    public void WriteResult_NoHeader_NoHeaderLine()
    {
        var store = new FileSystemDataStore(_dir);
        var table = MakeTable(hasHeader: false);

        string fileName = store.WriteResult("words.txt", table, "natural", "Значение", ascending: false);

        string[] lines = File.ReadAllLines(Path.Combine(_dir, "results", fileName));
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void ReadResult_RoundTrip()
    {
        var store = new FileSystemDataStore(_dir);
        var table = MakeTable();
        string fileName = store.WriteResult("fruits.csv", table, "direct", "Масса", true);

        TableData read = store.ReadResult(fileName, hasHeader: true);

        Assert.Equal(3, read.RowCount);
        Assert.Equal("Название", read.Columns[0].Name);
        Assert.Equal("Груша", read.Rows[0].Cells[0]);
    }

    [Fact]
    public void ListTables_FindsCsvAndTxt()
    {
        File.WriteAllText(Path.Combine(_dir, "a.csv"), "x\n1\n");
        File.WriteAllText(Path.Combine(_dir, "b.txt"), "y\n");
        Directory.CreateDirectory(Path.Combine(_dir, "results")); // подпапка не попадает в список
        File.WriteAllText(Path.Combine(_dir, "results", "r.csv"), "z\n");
        var store = new FileSystemDataStore(_dir);

        var tables = store.ListTables();

        Assert.Equal(["a.csv", "b.txt"], tables.Select(t => t.FileName).ToArray());
    }

    [Fact]
    public void PathTraversal_IsRejected()
    {
        var store = new FileSystemDataStore(_dir);

        Assert.Throws<TableFormatException>(() => store.ResultPath("../secret.txt"));
        Assert.Throws<TableFormatException>(() => store.ReadTable("a/b.csv", true));
    }
}
