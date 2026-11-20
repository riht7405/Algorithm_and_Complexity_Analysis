using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LabApp.Core;

/// <summary>Информация о файле-таблице.</summary>
public sealed record TableFileInfo(string FileName, long SizeBytes);

/// <summary>Информация о файле результата.</summary>
public sealed record ResultFileInfo(string FileName, long SizeBytes, DateTimeOffset CreatedAt);

/// <summary>
/// Хранилище данных: таблицы лежат в папке Data, результаты — в подпапке Data/results.
/// Новый формат файла достаточно положить в Data — приложение его подхватит.
/// </summary>
public interface IDataStore
{
    /// <summary>Список файлов-таблиц (csv/tsv/txt) в папке Data.</summary>
    IReadOnlyList<TableFileInfo> ListTables();

    /// <summary>Читает таблицу из файла папки Data.</summary>
    TableData ReadTable(string fileName, bool hasHeader);

    /// <summary>Записывает отсортированную таблицу в результирующий файл. Возвращает имя файла.</summary>
    string WriteResult(string sourceFileName, TableData table, string methodId, string keyName, bool ascending);

    /// <summary>Список файлов-результатов.</summary>
    IReadOnlyList<ResultFileInfo> ListResults();

    /// <summary>Читает файл результата (для показа таблицы результата).</summary>
    TableData ReadResult(string fileName, bool hasHeader);

    /// <summary>Полный путь к файлу результата (после проверки имени) — для скачивания.</summary>
    string ResultPath(string fileName);
}

public sealed class FileSystemDataStore : IDataStore
{
    private readonly string _root;
    private readonly string _resultsDir;

    public FileSystemDataStore(string root)
    {
        _root = root;
        _resultsDir = Path.Combine(root, "results");
        Directory.CreateDirectory(root);
    }

    public IReadOnlyList<TableFileInfo> ListTables()
    {
        return Directory.EnumerateFiles(_root)
            .Where(f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new TableFileInfo(Path.GetFileName(f), new FileInfo(f).Length))
            .ToList();
    }

    public TableData ReadTable(string fileName, bool hasHeader)
    {
        string safe = ValidateFileName(fileName);
        return TableFileReader.Read(Path.Combine(_root, safe), hasHeader);
    }

    public string WriteResult(string sourceFileName, TableData table, string methodId, string keyName, bool ascending)
    {
        Directory.CreateDirectory(_resultsDir);
        string baseName = Path.GetFileNameWithoutExtension(ValidateFileName(sourceFileName));
        string ext = Path.GetExtension(sourceFileName);
        if (string.IsNullOrEmpty(ext))
            ext = ".csv";
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string fileName =
            $"{baseName}_{methodId}_{Sanitize(keyName)}_{(ascending ? "asc" : "desc")}_{stamp}{ext.ToLowerInvariant()}";

        var sb = new StringBuilder();
        if (table.HasHeader)
            sb.AppendLine(JoinCells(table.Headers, table.Delimiter));
        foreach (var row in table.Rows)
            sb.AppendLine(JoinCells(row.Cells, table.Delimiter));
        File.WriteAllText(Path.Combine(_resultsDir, fileName), sb.ToString(), new UTF8Encoding(false));
        return fileName;
    }

    public IReadOnlyList<ResultFileInfo> ListResults()
    {
        if (!Directory.Exists(_resultsDir))
            return [];
        return Directory.EnumerateFiles(_resultsDir)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new ResultFileInfo(f.Name, f.Length, new DateTimeOffset(f.LastWriteTimeUtc)))
            .ToList();
    }

    public TableData ReadResult(string fileName, bool hasHeader)
    {
        return TableFileReader.Read(ResultPath(fileName), hasHeader);
    }

    public string ResultPath(string fileName)
    {
        string safe = ValidateFileName(fileName);
        string path = Path.Combine(_resultsDir, safe);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл результата «{safe}» не найден.", safe);
        return path;
    }

    private static string ValidateFileName(string fileName)
    {
        string name = Path.GetFileName(fileName.Trim());
        // Отсекаем обход каталогов: разделители и «..» запрещены ещё до обработки.
        if (name.Length == 0 || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            throw new TableFormatException("Некорректное имя файла.");
        return name;
    }

    private static string Sanitize(string text) =>
        Regex.Replace(text, @"[^\w\-]+", "_", RegexOptions.CultureInvariant).Trim('_');

    private static string JoinCells(IEnumerable<string> cells, char delimiter) =>
        string.Join(delimiter, cells.Select(c => Escape(c, delimiter)));

    /// <summary>Если в клетке есть разделитель или кавычка — оборачиваем в кавычки (стандарт CSV).</summary>
    private static string Escape(string cell, char delimiter)
    {
        bool needQuotes = cell.Contains(delimiter) || cell.Contains('"')
                          || cell.Contains('\n') || cell.Contains('\r');
        return needQuotes ? $"\"{cell.Replace("\"", "\"\"")}\"" : cell;
    }
}
