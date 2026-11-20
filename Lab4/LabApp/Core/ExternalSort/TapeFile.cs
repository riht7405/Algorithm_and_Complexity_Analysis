namespace LabApp.Core.ExternalSort;

/// <summary>
/// Модель файла-ленты, как настоящий файл на диске: читать можно только последовательно
/// (курсором), дописывать — только в конец. Произвольного доступа нет — так и работает
/// настоящая внешняя сортировка.
/// </summary>
public sealed class TapeFile
{
    private readonly List<int> _items = [];
    private int _readPos;

    public TapeFile(string name, string role)
    {
        Name = name;
        Role = role;
    }

    public string Name { get; }

    /// <summary>"source" — исходный файл, "tape" — вспомогательная лента, "output" — файл результата.</summary>
    public string Role { get; }

    public IReadOnlyList<int> Items => _items;
    public int Count => _items.Count;

    /// <summary>Сколько записей уже прочитано курсором.</summary>
    public int Cursor => _readPos;

    public bool HasNext => _readPos < _items.Count;

    /// <summary>Разбивка содержимого на серии (для отрисовки групп в интерфейсе). Может быть null.</summary>
    public int[][]? RunGroups { get; set; }

    /// <summary>Читает следующую запись и двигает курсор.</summary>
    public int ReadNext() => _items[_readPos++];

    /// <summary>Дописывает запись в конец файла.</summary>
    public void Append(int rowId) => _items.Add(rowId);

    /// <summary>Готовит файл к чтению с начала.</summary>
    public void ResetRead() => _readPos = 0;

    /// <summary>Стирает содержимое (файл готов принять новый результат).</summary>
    public void Clear()
    {
        _items.Clear();
        _readPos = 0;
        RunGroups = null;
    }
}
