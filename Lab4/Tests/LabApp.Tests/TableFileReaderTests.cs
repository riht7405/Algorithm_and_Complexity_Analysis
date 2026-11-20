using LabApp.Core;
using Xunit;

namespace LabApp.Tests;

/// <summary>Чтение таблиц из файлов: разделители, заголовки, типы столбцов, кавычки.</summary>
public class TableFileReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lab4-tests-" + Guid.NewGuid().ToString("N"));

    public TableFileReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteFile(string name, string content)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Csv_WithHeader_ParsesColumnsAndTypes()
    {
        string path = WriteFile("countries.csv",
            "Страна;Континент;Население\nФранция;Европа;68.2\nРоссия;Европа;146.2\n");

        var table = TableFileReader.Read(path, hasHeader: true);

        Assert.Equal("countries.csv", table.FileName);
        Assert.Equal(';', table.Delimiter);
        Assert.True(table.HasHeader);
        Assert.Equal(2, table.RowCount);
        Assert.Equal(3, table.ColumnCount);
        Assert.Equal("Страна", table.Columns[0].Name);
        Assert.Equal(ColumnType.Text, table.Columns[0].Type);
        Assert.Equal(ColumnType.Number, table.Columns[2].Type);
        Assert.Equal("Франция", table.Rows[0].Cells[0]);
        Assert.Equal(0, table.Rows[0].Id);
        Assert.Equal(1, table.Rows[1].Id);
    }

    [Fact]
    public void SingleColumnText_NoHeader()
    {
        string path = WriteFile("words.txt", "вишня\nяблоко\nабрикос\n");

        var table = TableFileReader.Read(path, hasHeader: false);

        Assert.Single(table.Columns);
        Assert.Equal(3, table.RowCount);
        Assert.Equal("вишня", table.Rows[0].Cells[0]);
        Assert.Equal('\0', table.Delimiter); // для простого текста разделителя нет
    }

    [Fact]
    public void QuotedCells_AreUnescaped()
    {
        string path = WriteFile("quoted.csv", "Имя;Город\n\"Иванов, И.\";\"Москва \"\"город\"\"\"\n");

        var table = TableFileReader.Read(path, hasHeader: true);

        Assert.Equal("Иванов, И.", table.Rows[0].Cells[0]);
        Assert.Equal("Москва \"город\"", table.Rows[0].Cells[1]);
    }

    [Fact]
    public void Numbers_WithComma_AreDetected()
    {
        string path = WriteFile("num.csv", "Значение\n1,5\n2,5\n10\n");

        var table = TableFileReader.Read(path, hasHeader: true);

        Assert.Equal(ColumnType.Number, table.Columns[0].Type);
    }

    [Fact]
    public void EmptyFile_Throws()
    {
        string path = WriteFile("empty.csv", "\n\n");

        Assert.Throws<TableFormatException>(() => TableFileReader.Read(path, hasHeader: true));
    }

    [Fact]
    public void Reordered_KeepsColumnsAndReordersRows()
    {
        string path = WriteFile("t.csv", "Имя\nA\nB\nC\n");
        var table = TableFileReader.Read(path, hasHeader: true);

        var reordered = table.Reordered([2, 0, 1]);

        Assert.Equal(["C", "A", "B"], reordered.Rows.Select(r => r.Cells[0]).ToArray());
        Assert.Equal(1, reordered.ColumnCount); // столбцы сохраняются
        Assert.Equal([2, 0, 1], reordered.Rows.Select(r => r.Id).ToArray()); // исходные Id не теряются
    }
}
