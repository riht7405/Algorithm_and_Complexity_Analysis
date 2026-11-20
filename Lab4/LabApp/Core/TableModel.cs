namespace LabApp.Core;

/// <summary>Тип значений столбца — определяется автоматически по содержимому файла.</summary>
public enum ColumnType
{
    Number,
    Text
}

/// <summary>Описание одного столбца таблицы.</summary>
public sealed record ColumnSpec(int Index, string Name, ColumnType Type);

/// <summary>Одна запись таблицы (строка файла). Id — исходный номер строки: по нему визуализация находит запись.</summary>
public sealed record RecordRow(int Id, string[] Cells);

/// <summary>Таблица, прочитанная из файла. Количество столбцов и строк может быть любым.</summary>
public sealed class TableData
{
    public string FileName { get; }
    public char Delimiter { get; }
    public bool HasHeader { get; }
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<ColumnSpec> Columns { get; }
    public IReadOnlyList<RecordRow> Rows { get; }

    public TableData(string fileName, char delimiter, bool hasHeader,
        IReadOnlyList<string> headers, IReadOnlyList<ColumnSpec> columns, IReadOnlyList<RecordRow> rows)
    {
        FileName = fileName;
        Delimiter = delimiter;
        HasHeader = hasHeader;
        Headers = headers;
        Columns = columns;
        Rows = rows;
    }

    public int ColumnCount => Headers.Count;
    public int RowCount => Rows.Count;

    /// <summary>Находит столбец по имени (без учёта регистра). Возвращает -1, если столбца нет.</summary>
    public int IndexOfColumn(string name)
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            if (string.Equals(Columns[i].Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>Новая таблица с теми же столбцами, но строки переставлены в порядке rowIds.</summary>
    public TableData Reordered(IReadOnlyList<int> rowIds)
    {
        var rows = new List<RecordRow>(rowIds.Count);
        foreach (int id in rowIds)
            rows.Add(Rows[id]);
        return new TableData(FileName, Delimiter, HasHeader, Headers, Columns, rows);
    }
}
