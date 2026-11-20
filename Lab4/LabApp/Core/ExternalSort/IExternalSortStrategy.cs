namespace LabApp.Core.ExternalSort;

/// <summary>
/// Одна стратегия внешней сортировки. Новый метод сортировки добавляется реализацией
/// этого интерфейса и одной строкой регистрации в Program.cs — остальной код не меняется.
/// </summary>
public interface IExternalSortStrategy
{
    /// <summary>Идентификатор метода (его передаёт интерфейс пользователя).</summary>
    string Id { get; }

    /// <summary>Название метода для пользователя.</summary>
    string Title { get; }

    /// <summary>Сколько значений метод держит в памяти одновременно.</summary>
    int MemorySlots { get; }

    /// <summary>Короткое объяснение метода «для 6-классника».</summary>
    string ShortInfo { get; }

    /// <summary>Выполняет сортировку и возвращает пошаговую трассировку.</summary>
    SortTrace Execute(TableData table, int keyColumn, bool ascending);
}

/// <summary>Описание метода сортировки для списка в интерфейсе.</summary>
public sealed record StrategyInfo(string Id, string Title, int MemorySlots, string ShortInfo);
