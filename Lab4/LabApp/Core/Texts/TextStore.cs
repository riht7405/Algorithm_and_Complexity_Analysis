using System.Globalization;
using System.Text;
using LabApp.Core;
using LabApp.Core.WordSort;

namespace LabApp.Core.Texts;

/// <summary>Информация о файле-тексте.</summary>
public sealed record TextFileInfo(string FileName, long SizeBytes);

/// <summary>
/// Хранилище текстов (задание 3): тексты лежат в папке Data/texts (один файл — один текст),
/// результаты сортировки слов пишутся в общую папку результатов Data/results.
/// Новый текст достаточно положить в Data/texts — приложение его подхватит.
/// </summary>
public interface ITextStore
{
    /// <summary>Список текстовых файлов (txt) в папке Data/texts.</summary>
    IReadOnlyList<TextFileInfo> ListTexts();

    /// <summary>Читает текст из файла папки Data/texts.</summary>
    string ReadText(string fileName);

    /// <summary>Сохраняет сгенерированный текст нужной длины. Возвращает имя файла.</summary>
    string SaveGenerated(int wordCount, string content);

    /// <summary>Записывает подсчёт слов в результирующий файл. Возвращает имя файла.</summary>
    string WriteCountsResult(string sourceFileName, string methodId, bool ascending, IReadOnlyList<WordCount> counts);

    /// <summary>Полный путь к файлу результата (после проверки имени) — для скачивания.</summary>
    string ResultPath(string fileName);

    /// <summary>При первом запуске создаёт тексты стандартной длины из задания (100…5000 слов).</summary>
    void EnsureDefaults();
}

public sealed class FileSystemTextStore : ITextStore
{
    private readonly string _textsDir;
    private readonly string _resultsDir;

    public FileSystemTextStore(string textsDir, string resultsDir)
    {
        _textsDir = textsDir;
        _resultsDir = resultsDir;
        Directory.CreateDirectory(textsDir);
        Directory.CreateDirectory(resultsDir);
    }

    public IReadOnlyList<TextFileInfo> ListTexts() =>
        Directory.EnumerateFiles(_textsDir, "*.txt")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new TextFileInfo(Path.GetFileName(f), new FileInfo(f).Length))
            .ToList();

    public string ReadText(string fileName)
    {
        string path = Path.Combine(_textsDir, ValidateFileName(fileName));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Текст «{fileName}» не найден в папке Data/texts.", fileName);
        return File.ReadAllText(path);
    }

    public string SaveGenerated(int wordCount, string content)
    {
        string fileName = TextGenerator.FileNameFor(wordCount);
        File.WriteAllText(Path.Combine(_textsDir, fileName), content, new UTF8Encoding(false));
        return fileName;
    }

    public string WriteCountsResult(string sourceFileName, string methodId, bool ascending, IReadOnlyList<WordCount> counts)
    {
        Directory.CreateDirectory(_resultsDir);
        string baseName = Path.GetFileNameWithoutExtension(ValidateFileName(sourceFileName));
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string fileName =
            $"words_{baseName}_{methodId}_{(ascending ? "asc" : "desc")}_{stamp}.csv";

        var sb = new StringBuilder();
        sb.AppendLine("слово;вхождений");
        foreach (var c in counts)
            sb.Append(c.Word).Append(';').AppendLine(c.Count.ToString(CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(_resultsDir, fileName), sb.ToString(), new UTF8Encoding(false));
        return fileName;
    }

    public string ResultPath(string fileName)
    {
        string path = Path.Combine(_resultsDir, ValidateFileName(fileName));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл результата «{fileName}» не найден.", fileName);
        return path;
    }

    public void EnsureDefaults()
    {
        // Каждый подготовленный текст создаём, если его ещё нет: так набор 5…100 слов
        // появляется и в пустой папке, и рядом с уже существующими файлами пользователя.
        foreach (int length in TextGenerator.DefaultLengths)
        {
            string path = Path.Combine(_textsDir, TextGenerator.FileNameFor(length));
            if (!File.Exists(path))
                SaveGenerated(length, TextGenerator.Generate(length, seed: length * 7919));
        }
    }

    private static string ValidateFileName(string fileName)
    {
        string name = Path.GetFileName(fileName.Trim());
        // Отсекаем обход каталогов: разделители и «..» запрещены ещё до обработки.
        if (name.Length == 0 || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            throw new TableFormatException("Некорректное имя файла.");
        return name;
    }
}

/// <summary>
/// Какие файлы показывать в списке «Готовые текста»: подготовленные тексты 5…100 слов
/// и свои небольшие файлы пользователя. Устаревшие сгенерированные файлы большого объёма
/// (text_500.txt, text_1000.txt…) в демонстрацию не попадают — для них есть замеры времени.
/// </summary>
public static class TextCatalog
{
    private static readonly System.Text.RegularExpressions.Regex GeneratedPattern =
        new(@"^text_(\d+)\.txt$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static bool IsVisibleInDemo(string fileName, int maxWords)
    {
        var match = GeneratedPattern.Match(Path.GetFileName(fileName));
        if (!match.Success) return true; // файл пользователя — показываем
        return int.TryParse(match.Groups[1].Value, out int n) && n <= maxWords;
    }
}
