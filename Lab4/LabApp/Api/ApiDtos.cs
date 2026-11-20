using LabApp.Core;
using LabApp.Core.ExternalSort;

namespace LabApp.Api;

// ---------- Запросы ----------

/// <summary>Запрос превью таблицы (для показа исходной таблицы).</summary>
public sealed record PreviewRequest(string FileName, bool HasHeader = true);

/// <summary>Запуск сортировки.</summary>
public sealed record SortRequest(string FileName, bool HasHeader, string Method, string KeyColumn, bool Ascending);

// ---------- Ответы ----------

public sealed record TableListDto(string FileName, long SizeBytes);
public sealed record ResultListDto(string FileName, long SizeBytes, DateTimeOffset CreatedAt);
public sealed record ColumnDto(int Index, string Name, string Type);
public sealed record RowDto(int Id, string[] Cells);

/// <summary>Превью таблицы: столбцы + строки (для отрисовки исходной таблицы).</summary>
public sealed record PreviewDto(
    string FileName,
    bool HasHeader,
    string Delimiter,
    IReadOnlyList<ColumnDto> Columns,
    IReadOnlyList<RowDto> Rows);

/// <summary>Снимок одной ленты на шаге сортировки.</summary>
public sealed record TapeDto(string Name, string Role, int[] Ids, int Cursor, int[][]? Runs);

/// <summary>Один шаг визуализации.</summary>
public sealed record StepDto(
    int Index,
    string Kind,
    string Phase,
    int Pass,
    string? PassTitle,
    string Desc,
    string? LogLine,
    string? LogKind,
    int[] MemoryIds,
    string MemoryNote,
    List<TapeDto>? Snap,
    int Comparisons,
    int Moves,
    int Passes);

public sealed record StatsDto(int Comparisons, int Moves, int Passes, int TotalSteps, int InputRows);

/// <summary>Полный ответ на запуск сортировки: вся трассировка + итоговый файл.</summary>
public sealed record SortResponse(
    string FileName,
    bool HasHeader,
    string Method,
    string MethodTitle,
    int MemorySlots,
    int KeyColumn,
    string KeyColumnName,
    bool Ascending,
    int LabelColumn,
    IReadOnlyList<ColumnDto> Columns,
    IReadOnlyList<RowDto> Rows,
    List<StepDto> Steps,
    StatsDto Stats,
    string? ResultFileName);

// ---------- Преобразования ----------

public static class DtoMapper
{
    public static PreviewDto ToPreview(TableData table) => new(
        table.FileName,
        table.HasHeader,
        table.Delimiter == '\t' ? "\\t (табуляция)" : table.Delimiter.ToString(),
        table.Columns.Select(c => new ColumnDto(c.Index, c.Name, c.Type == ColumnType.Number ? "number" : "text")).ToList(),
        table.Rows.Select(r => new RowDto(r.Id, r.Cells)).ToList());

    public static SortResponse ToResponse(ExternalSortResult run)
    {
        var trace = run.Trace;
        var columns = run.Source.Columns
            .Select(c => new ColumnDto(c.Index, c.Name, c.Type == ColumnType.Number ? "number" : "text"))
            .ToList();
        var rows = run.Source.Rows.Select(r => new RowDto(r.Id, r.Cells)).ToList();
        int labelColumn = SortTraceBuilder.PickLabelColumn(run.Source, run.KeyColumn);

        var steps = trace.Steps.Select(s => new StepDto(
                s.Index, s.Kind.ToString(), s.Phase, s.Pass, s.PassTitle,
                s.Desc, s.LogLine, s.LogKind, s.MemoryIds, s.MemoryNote,
                s.Snap?.Select(t => new TapeDto(t.Name, t.Role, t.Ids, t.Cursor, t.Runs)).ToList(),
                s.Comparisons, s.Moves, s.Passes))
            .ToList();

        return new SortResponse(
            run.Source.FileName,
            run.Source.HasHeader,
            run.Strategy.Id,
            run.Strategy.Title,
            run.Strategy.MemorySlots,
            run.KeyColumn,
            run.Source.Columns[run.KeyColumn].Name,
            run.Ascending,
            labelColumn,
            columns,
            rows,
            steps,
            new StatsDto(trace.TotalComparisons, trace.TotalMoves, trace.TotalPasses,
                trace.Steps.Count, run.Source.RowCount),
            trace.ResultFileName);
    }
}
