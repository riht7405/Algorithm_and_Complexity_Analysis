using LabApp.Core.Texts;
using LabApp.Core.WordSort;

namespace LabApp.Api;

// ---------- Запросы ----------

/// <summary>Запрос превью текста (для показа исходного текста).</summary>
public sealed record TextPreviewRequest(string FileName);

/// <summary>Запуск сортировки подготовленного текста.</summary>
public sealed record WordSortRequest(string FileName, string Method, bool Ascending = true);

/// <summary>Сортировка случайного текста: seed не задан — новый случайный текст;
/// seed передан — тот же текст пересортировывается другим способом.</summary>
public sealed record RandomSortRequest(string Method, bool Ascending = true, int WordCount = 25, int? Seed = null);

/// <summary>Замер времени: объём текста задаётся прямо здесь, независимо от настроек демонстрации.</summary>
public sealed record WordBenchRequest(string Algorithm, int Runs = 10, int WordCount = 1000);

// ---------- Ответы ----------

public sealed record TextListDto(string FileName, long SizeBytes);

/// <summary>Превью текста: первые строки + сколько в нём слов.</summary>
public sealed record TextPreviewDto(
    string FileName, int TotalWords, int UniqueWords, string Preview, bool PreviewTruncated);

public sealed record WordEntryDto(int Id, string Text);

/// <summary>Снимок одной корзины ABC-сортировки на шаге.</summary>
public sealed record BucketDto(string Letter, int[] Ids);

/// <summary>Один шаг визуализации сортировки слов.</summary>
public sealed record WordStepDto(
    int Index,
    string Kind,
    string Phase,
    int Pass,
    string? PassTitle,
    string Desc,
    string? LogLine,
    string? LogKind,
    int[]? ArrayIds,
    int[]? Highlights,
    int? PivotId,
    int[]? RangeIds,
    int[]? Placed,
    int[] MemoryIds,
    string MemoryNote,
    List<BucketDto>? Buckets,
    long Comparisons,
    long Moves,
    int Passes);

public sealed record WordStatsDto(long Comparisons, long Moves, int Passes, int TotalSteps);

public sealed record WordCountRowDto(int Rank, string Word, int Count);

/// <summary>Полный ответ на запуск сортировки слов: трассировка + подсчёт по всему тексту + файл.</summary>
public sealed record WordSortResponse(
    string FileName,
    string Method,
    string MethodTitle,
    bool Ascending,
    int WordCount,
    int UniqueWords,
    string PassesLabel,
    int MemorySlots,
    IReadOnlyList<WordEntryDto> Words,
    List<WordStepDto> Steps,
    WordStatsDto Stats,
    string? ResultFileName,
    IReadOnlyList<WordCountRowDto> Counts,
    int CountsUniqueWords,
    bool CountsTruncated,
    string CountNote,
    string? SourcePreview,
    int? RandomSeed);

public sealed record WordBenchRowDto(
    string Algorithm,
    string AlgorithmTitle,
    int WordCount,
    string Source,
    int Runs,
    double AvgMs,
    double MinMs,
    double MaxMs,
    long Comparisons,
    long Moves,
    int Passes,
    IReadOnlyList<double> Times);

// ---------- Преобразования ----------

public static class WordDtoMapper
{
    private const string CountNote =
        "Подсчёт выполнен по всему тексту: отсортированный список слов пройден насквозь, " +
        "у каждого слова посчитано число вхождений. Полный список — в файле результата.";

    public static TextPreviewDto ToPreview(string fileName, string text)
    {
        var words = TextNormalizer.ExtractWords(text);
        const int previewChars = 1200;
        bool truncated = text.Length > previewChars;
        return new TextPreviewDto(
            fileName,
            words.Count,
            words.Distinct().Count(),
            truncated ? text[..previewChars] : text,
            truncated);
    }

    public static WordSortResponse ToResponse(WordSortRun run)
    {
        const int maxCountRows = WordSorter.MaxCountRows;
        bool truncated = run.Counts.Count > maxCountRows;
        var counts = run.Counts
            .Take(maxCountRows)
            .Select((c, i) => new WordCountRowDto(i + 1, c.Word, c.Count))
            .ToList();

        var steps = run.Trace.Steps.Select(s => new WordStepDto(
                s.Index, s.Kind.ToString(), s.Phase, s.Pass, s.PassTitle,
                s.Desc, s.LogLine, s.LogKind,
                s.ArrayIds, s.Highlights, s.PivotId, s.RangeIds, s.Placed,
                s.MemoryIds, s.MemoryNote,
                s.Buckets?.Select(t => new BucketDto(t.Letter, t.Ids)).ToList(),
                s.Comparisons, s.Moves, s.Passes))
            .ToList();

        return new WordSortResponse(
            run.FileName,
            run.Algorithm.Id,
            run.Algorithm.Title,
            run.Ascending,
            run.WordCount,
            run.Counts.Count,
            run.Algorithm.PassesLabel,
            run.Algorithm.MemorySlots,
            run.Words.Select(w => new WordEntryDto(w.Id, w.Text)).ToList(),
            steps,
            new WordStatsDto(run.Trace.TotalComparisons, run.Trace.TotalMoves,
                run.Trace.TotalPasses, run.Trace.Steps.Count),
            run.ResultFileName,
            counts,
            run.Counts.Count,
            truncated,
            CountNote,
            run.SourcePreview,
            run.RandomSeed);
    }

    public static WordBenchRowDto ToDto(WordBenchResult r) => new(
        r.AlgorithmId, r.AlgorithmTitle, r.WordCount, r.Source,
        r.Runs, r.AvgMs, r.MinMs, r.MaxMs, r.Comparisons, r.Moves, r.Passes, r.Times);
}
