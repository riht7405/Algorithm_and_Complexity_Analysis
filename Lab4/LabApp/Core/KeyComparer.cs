using System.Globalization;

namespace LabApp.Core;

/// <summary>
/// Сравнение записей по ключевому атрибуту.
/// Числовые столбцы сравниваются как числа, текстовые — «по алфавиту» (русская культура ru-RU).
/// CompareRaw возвращает порядок «по возрастанию»: меньше 0 — запись A должна идти раньше B.
/// Пустые значения всегда попадают в конец списка, при равенстве записи остаются в исходном порядке.
/// </summary>
public sealed class KeyComparer
{
    private static readonly CompareInfo Russian = CultureInfo.GetCultureInfo("ru-RU").CompareInfo;

    private readonly TableData _table;
    private readonly double?[] _numbers; // распарсенные числа ключевого столбца, по Id строки

    public int KeyColumn { get; }
    public bool Ascending { get; }
    public bool IsNumeric { get; }

    public KeyComparer(TableData table, int keyColumn, bool ascending)
    {
        _table = table;
        KeyColumn = keyColumn;
        Ascending = ascending;
        IsNumeric = table.Columns[keyColumn].Type == ColumnType.Number;

        _numbers = new double?[table.RowCount];
        if (IsNumeric)
        {
            foreach (var row in table.Rows)
            {
                string cell = KeyCell(row.Id);
                _numbers[row.Id] = cell.Length > 0 && TableFileReader.TryParseNumber(cell, out double v) ? v : null;
            }
        }
    }

    /// <summary>Сырое сравнение «по возрастанию»: &lt;0 — A раньше B (независимо от направления сортировки).</summary>
    public int CompareRaw(int rowIdA, int rowIdB)
    {
        if (IsNumeric)
        {
            double? a = _numbers[rowIdA];
            double? b = _numbers[rowIdB];
            if (a is null && b is null) return 0;
            if (a is null) return 1;  // пустое значение — всегда в конце
            if (b is null) return -1;
            return a.Value.CompareTo(b.Value);
        }
        return Russian.Compare(KeyCell(rowIdA), KeyCell(rowIdB), CompareOptions.IgnoreCase);
    }

    /// <summary>Числовое значение ключа (или null, если столбец текстовый / значение пустое).</summary>
    public double? Number(int rowId) => IsNumeric ? _numbers[rowId] : null;

    /// <summary>Значение ключа для показа пользователю.</summary>
    public string KeyText(int rowId)
    {
        string cell = KeyCell(rowId);
        if (!IsNumeric || cell.Length == 0)
            return cell.Length == 0 ? "пусто" : Truncate(cell, 24);
        double? n = _numbers[rowId];
        return n is null ? "пусто" : Format(n.Value);
    }

    public string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Truncate(string text, int max)
    {
        text = text.Trim();
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    private string KeyCell(int rowId)
    {
        var row = _table.Rows[rowId];
        return KeyColumn < row.Cells.Length ? row.Cells[KeyColumn] : string.Empty;
    }
}
