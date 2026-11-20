namespace LabApp.Core.ExternalSort;

/// <summary>
/// Фасад внешней сортировки: выбирает стратегию, выполняет её и сохраняет
/// отсортированную таблицу в результирующий файл.
/// </summary>
public sealed class ExternalSorter
{
    /// <summary>Ограничение пошаговой визуализации: больше записей — огромная трассировка.</summary>
    public const int MaxTraceRows = 200;

    private readonly IDataStore _store;
    private readonly Dictionary<string, IExternalSortStrategy> _strategies;

    public ExternalSorter(IDataStore store, IEnumerable<IExternalSortStrategy> strategies)
    {
        _store = store;
        _strategies = strategies.ToDictionary(s => s.Id);
    }

    /// <summary>Список доступных методов сортировки (для выбора в интерфейсе).</summary>
    public IReadOnlyList<StrategyInfo> ListMethods() =>
        _strategies.Values
            .Select(s => new StrategyInfo(s.Id, s.Title, s.MemorySlots, s.ShortInfo))
            .ToList();

    /// <summary>Выполняет сортировку и записывает результат в файл. Бросает TableFormatException при проблемах.</summary>
    public ExternalSortResult Run(TableData table, string methodId, string keyColumn, bool ascending)
    {
        if (table.RowCount == 0)
            throw new TableFormatException("Таблица пустая — сортировать нечего.");
        if (table.RowCount > MaxTraceRows)
            throw new TableFormatException(
                $"Для пошаговой визуализации берём до {MaxTraceRows} записей (здесь {table.RowCount}). Сократи файл.");
        if (!_strategies.TryGetValue(methodId, out var strategy))
            throw new TableFormatException($"Неизвестный метод сортировки: «{methodId}».");

        int keyIndex = table.IndexOfColumn(keyColumn);
        if (keyIndex < 0)
            throw new TableFormatException($"В таблице «{table.FileName}» нет столбца «{keyColumn}».");

        SortTrace trace = strategy.Execute(table, keyIndex, ascending);
        TableData result = table.Reordered(trace.FinalOrder);
        string resultFile = _store.WriteResult(
            table.FileName, result, strategy.Id, table.Columns[keyIndex].Name, ascending);
        trace.ResultFileName = resultFile;

        return new ExternalSortResult(table, trace, result, resultFile, keyIndex, ascending, strategy);
    }
}

/// <summary>Итог одного запуска сортировки.</summary>
public sealed record ExternalSortResult(
    TableData Source,
    SortTrace Trace,
    TableData Result,
    string ResultFileName,
    int KeyColumn,
    bool Ascending,
    IExternalSortStrategy Strategy);
