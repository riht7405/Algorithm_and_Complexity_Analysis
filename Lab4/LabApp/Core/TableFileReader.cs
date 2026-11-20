using System.Globalization;
using System.Text;

namespace LabApp.Core;

/// <summary>
/// Читает таблицу из файла: CSV/TSV с разделителем «;», «,» или табуляцией,
/// либо обычный текстовый файл «одна строка — одна запись».
/// Количество столбцов может быть любым, определяется автоматически.
/// </summary>
public static class TableFileReader
{
    public static TableData Read(string path, bool hasHeader)
    {
        string fileName = Path.GetFileName(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл «{fileName}» не найден в папке Data.", path);

        // Пустые строки пропускаем, в первой строке убираем BOM (если файл сохранён «Блокнотом»).
        var lines = File.ReadAllLines(path)
            .Select((line, i) => i == 0 ? line.TrimStart('\uFEFF') : line)
            .Where(line => line.Trim().Length > 0)
            .ToList();

        if (lines.Count == 0)
            throw new TableFormatException($"Файл «{fileName}» пустой — сортировать нечего.");

        char delimiter = DetectDelimiter(lines[0]);

        // Заголовок: если попросили и строк больше одной, первую строку съедаем как заголовок.
        string[] headers;
        var dataLines = lines;
        if (hasHeader && lines.Count > 1)
        {
            var headerCells = delimiter == '\0'
                ? new List<string> { lines[0].Trim() }
                : ParseLine(lines[0], delimiter);
            dataLines = lines.Skip(1).ToList();
            headers = headerCells
                .Select((name, i) => string.IsNullOrWhiteSpace(name) ? $"Поле {i + 1}" : name.Trim())
                .ToArray();
        }
        else
        {
            int width = delimiter == '\0' ? 1 : ParseLine(lines[0], delimiter).Count;
            headers = Enumerable.Range(1, Math.Max(width, 1)).Select(i => $"Поле {i}").ToArray();
        }

        string[][] cells;
        if (delimiter == '\0')
        {
            cells = dataLines.Select(line => new[] { line.Trim() }).ToArray();
        }
        else
        {
            var parsed = dataLines.Select(line => ParseLine(line, delimiter)).ToList();
            int width = Math.Max(headers.Length, parsed.Count > 0 ? parsed.Max(c => c.Count) : 0);
            foreach (var row in parsed)
            {
                while (row.Count < width) row.Add(string.Empty);
                if (row.Count > width) row.RemoveRange(width, row.Count - width);
            }
            // Если в данных столбцов больше, чем в заголовке, добиваем заголовок.
            if (headers.Length < width)
                headers = headers
                    .Concat(Enumerable.Range(headers.Length + 1, width - headers.Length).Select(i => $"Поле {i}"))
                    .ToArray();
            cells = parsed.Select(r => r.ToArray()).ToArray();
        }

        if (cells.Length == 0)
            throw new TableFormatException($"В файле «{fileName}» нет строк с данными.");

        var rows = new List<RecordRow>(cells.Length);
        for (int i = 0; i < cells.Length; i++)
            rows.Add(new RecordRow(i, cells[i]));

        var columns = new List<ColumnSpec>(headers.Length);
        for (int i = 0; i < headers.Length; i++)
            columns.Add(new ColumnSpec(i, headers[i], DetectType(rows, i)));

        return new TableData(fileName, delimiter, hasHeader && lines.Count > 1, headers, columns, rows);
    }

    /// <summary>Определяем разделитель по первой строке: чаще всего встречаются «;», «,» или табуляция.</summary>
    private static char DetectDelimiter(string firstLine)
    {
        var candidates = new (char Symbol, int Count)[]
        {
            (';', firstLine.Count(c => c == ';')),
            (',', firstLine.Count(c => c == ',')),
            ('\t', firstLine.Count(c => c == '\t')),
        };
        (char Symbol, int Count) best = candidates.OrderByDescending(c => c.Count).First();
        return best.Count > 0 ? best.Symbol : '\0';
    }

    /// <summary>Разбор одной строки CSV с поддержкой кавычек «"..."» и удвоенных кавычек внутри.</summary>
    private static List<string> ParseLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"' && sb.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                cells.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        cells.Add(sb.ToString().Trim());
        return cells;
    }

    /// <summary>Числовой ли столбец: все непустые значения должны разбираться как числа.</summary>
    private static ColumnType DetectType(IReadOnlyList<RecordRow> rows, int column)
    {
        bool anyValue = false, allNumeric = true;
        foreach (var row in rows)
        {
            string cell = column < row.Cells.Length ? row.Cells[column].Trim() : string.Empty;
            if (cell.Length == 0)
                continue;
            anyValue = true;
            if (!TryParseNumber(cell, out _))
            {
                allNumeric = false;
                break;
            }
        }
        return anyValue && allNumeric ? ColumnType.Number : ColumnType.Text;
    }

    /// <summary>Число можно записать и с точкой, и с запятой (68.5 и 68,5).</summary>
    public static bool TryParseNumber(string text, out double value)
    {
        text = text.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;
        return double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
