using LabApp.Core.ArraySort;

namespace LabApp.Api;

// ---------- Запросы ----------

/// <summary>Генерация случайного массива для демонстрации: seed не задан — новый массив;
/// seed передан — тот же массив (пересортировка другим алгоритмом/в другом порядке).</summary>
public sealed record ArrayRandomRequest(int Count, int? Seed = null);

/// <summary>Запуск внутренней сортировки: массив передаётся текстом (числа через пробел,
/// запятую или с новой строки) и разбирается на сервере — единая точка валидации.</summary>
public sealed record ArraySortRequest(string Values, string Method, bool Ascending = true);

/// <summary>Замер времени: объём массива задаётся прямо здесь, независимо от настроек демонстрации.</summary>
public sealed record ArrayBenchRequest(string Algorithm, int Runs = 5, int ElementCount = 1000);

// ---------- Ответы ----------

public sealed record ArrayRandomResponse(int[] Values, int Seed);

/// <summary>Число массива с исходным номером: Id не меняется при перестановках.</summary>
public sealed record ArrayEntryDto(int Id, int Value);

/// <summary>Один шаг визуализации внутренней сортировки.</summary>
public sealed record ArrayStepDto(
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
    int? MarkId,
    int[]? RangeIds,
    int[]? Placed,
    int[]? BufferIds,
    int[]? SeriesA,
    int[]? SeriesB,
    int[]? Tree,
    int? TreeNode,
    int[] MemoryIds,
    string MemoryNote,
    long Comparisons,
    long Moves,
    int Passes);

public sealed record ArrayStatsDto(long Comparisons, long Moves, int Passes, int TotalSteps);

/// <summary>Полный ответ на запуск внутренней сортировки: исходный массив + трассировка.</summary>
public sealed record ArraySortResponse(
    string Method,
    string MethodTitle,
    string Group,
    bool Ascending,
    int ElementCount,
    string PassesLabel,
    int MemorySlots,
    IReadOnlyList<ArrayEntryDto> Elements,
    List<ArrayStepDto> Steps,
    ArrayStatsDto Stats,
    int[] FinalOrder);

public sealed record ArrayBenchRowDto(
    string Algorithm,
    string AlgorithmTitle,
    int ElementCount,
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

public static class ArrayDtoMapper
{
    public static ArraySortResponse ToResponse(ArraySortRun run)
    {
        var steps = run.Trace.Steps.Select(s => new ArrayStepDto(
                s.Index, s.Kind.ToString(), s.Phase, s.Pass, s.PassTitle,
                s.Desc, s.LogLine, s.LogKind,
                s.ArrayIds, s.Highlights, s.MarkId, s.RangeIds, s.Placed, s.BufferIds,
                s.SeriesA, s.SeriesB, s.Tree, s.TreeNode,
                s.MemoryIds, s.MemoryNote,
                s.Comparisons, s.Moves, s.Passes))
            .ToList();

        return new ArraySortResponse(
            run.Algorithm.Id,
            run.Algorithm.Title,
            run.Algorithm.Group,
            run.Ascending,
            run.Elements.Length,
            run.Algorithm.PassesLabel,
            run.Algorithm.MemorySlots,
            run.Elements.Select(w => new ArrayEntryDto(w.Id, w.Value)).ToList(),
            steps,
            new ArrayStatsDto(run.Trace.TotalComparisons, run.Trace.TotalMoves,
                run.Trace.TotalPasses, run.Trace.Steps.Count),
            run.Trace.FinalOrder);
    }

    public static ArrayBenchRowDto ToDto(ArrayBenchResult r) => new(
        r.AlgorithmId, r.AlgorithmTitle, r.ElementCount, r.Source,
        r.Runs, r.AvgMs, r.MinMs, r.MaxMs, r.Comparisons, r.Moves, r.Passes, r.Times);
}
