namespace LabApp.Core.ArraySort;

/// <summary>
/// Строит пошаговую трассировку внутренней сортировки: снимки массива и буфера слияния,
/// «что в памяти», объяснения простым языком и строки журнала сравнений/перестановок.
/// Счётчики живут здесь — один источник правды и для визуализации, и для быстрых замеров времени.
/// В тихом режиме (active: false) шаги не собираются и строки не строятся — остаются только счётчики,
/// поэтому замеры времени не тратят время на строки.
/// Буфер слияния трассировщик ведёт сам: алгоритм пишет числа в массив-буфер и на каждой записи
/// вызывает Write — трассировщик добавляет Id числа в снимок буфера; CopyBack фиксирует перепись
/// буфера обратно в массив и очищает буфер.
/// </summary>
public sealed class ArraySortTraceBuilder
{
    /// <summary>Рабочий массив алгоритма: builder делает его снимки, алгоритм его переставляет.</summary>
    private readonly ArrayEntry[] _work;
    private readonly bool _active;
    private readonly List<ArraySortStep> _steps = [];
    private readonly HashSet<int> _placed = [];     // числа, стоящие на финальных местах
    private readonly List<int> _buffer = [];        // снимок буфера слияния (Id чисел по порядку записи)
    private bool _placedDirty = true;               // первый шаг должен включить список placed

    private long _comparisons;
    private long _moves;
    private int _autoPass;                          // авто-нумерация проходов
    private string _phase = "Подготовка";
    private int _passNo;
    private string? _passTitle;

    public ArraySortTraceBuilder(ArrayEntry[] work, bool active)
    {
        _work = work;
        _active = active;
    }

    /// <summary>false — тихий режим: шаги не записываются, описания можно не строить.</summary>
    public bool Active => _active;
    public long Comparisons => _comparisons;
    public long Moves => _moves;
    public int Passes => _autoPass;

    /// <summary>Большой этап: знакомство с задачей, финиш.</summary>
    public void Phase(string title, string desc)
    {
        _phase = title;
        AddStep(ArrayStepKind.PhaseStart, desc, $"=== {title} ===", "phase",
            memory: [], note: "Память пока пуста — ещё ничего не сравнивали.");
    }

    /// <summary>Начало прохода с авто-номером: новый выбор минимума, вставка, слияние.
    /// desc может быть null в тихом режиме (замер времени) — шаг всё равно не пишется.
    /// clearBuffer — на старте прохода буфер прошлого прохода неактуален.</summary>
    public void PassNext(string phase, string title, string? desc, int[]? rangeIds, string memoryNote,
        bool clearBuffer = false)
    {
        _phase = phase;
        _autoPass++;
        _passNo = _autoPass;
        _passTitle = $"Проход {_autoPass}: {title}";
        AddStep(ArrayStepKind.PassStart, desc, $"--- {_passTitle} ---", "phase",
            memory: [], note: memoryNote, rangeIds: rangeIds,
            buffer: clearBuffer ? [] : null);
    }

    /// <summary>Сравнили два числа (стоят в массиве на позициях i и j).
    /// markIndex — позиция особого числа (текущий минимум в SelectSort), подсвечивается отдельно.
    /// memory — какие числа показать в памяти (по умолчанию оба сравниваемых; во вставках
    /// в памяти держится «взятое» число, поэтому список передаёт алгоритм).</summary>
    public void Compare(int i, int j, string? desc, string? log, string memoryNote,
        int? markIndex = null, int[]? memory = null)
    {
        _comparisons++;
        if (!_active) return;
        AddStep(ArrayStepKind.Compare,
            desc!, log,
            "compare",
            memory ?? [_work[i].Id, _work[j].Id],
            note: memoryNote,
            highlights: [_work[i].Id, _work[j].Id],
            markId: markIndex is null ? null : _work[markIndex.Value].Id);
    }

    /// <summary>Массив уже переставлен алгоритмом — фиксируем обмен позиций i и j снимком.</summary>
    public void Swap(int i, int j, string? desc, string? log, string memoryNote)
    {
        _moves++;
        if (!_active) return;
        AddStep(ArrayStepKind.Swap, desc!, log, "move",
            memory: [_work[i].Id, _work[j].Id], note: memoryNote,
            highlights: [_work[i].Id, _work[j].Id], snap: true);
    }

    /// <summary>Сдвиг числа из позиции from в позицию to (вставки). Массив уже сдвинут.</summary>
    public void Shift(int from, int to, string? desc, string? log, string memoryNote)
    {
        _moves++;
        if (!_active) return;
        AddStep(ArrayStepKind.Shift, desc!, log, "move",
            memory: [_work[to].Id], note: memoryNote,
            highlights: [_work[to].Id, _work[from].Id], snap: true);
    }

    /// <summary>Число из позиции srcIndex записано в буфер слияния. Буфер-снимок растёт на одно число.</summary>
    public void Write(int srcIndex, string? desc, string? log, string memoryNote)
    {
        _moves++;
        if (!_active) return; // снимок буфера нужен только для трассировки
        _buffer.Add(_work[srcIndex].Id);
        AddStep(ArrayStepKind.Write, desc!, log, "move",
            memory: [_work[srcIndex].Id], note: memoryNote,
            highlights: [_work[srcIndex].Id], buffer: [.. _buffer]);
    }

    /// <summary>Буфер переписан обратно в массив (конец прохода слияния). Массив уже переписан —
    /// фиксируем снимком; буфер очищается.</summary>
    public void CopyBack(int count, string? desc, string? log, string memoryNote)
    {
        _moves += count;
        if (!_active) return;
        _buffer.Clear();
        AddStep(ArrayStepKind.CopyBack, desc!, log, "move",
            memory: [], note: memoryNote, snap: true, buffer: []);
    }

    /// <summary>«Взятое» число опустилось на своё место (вставки): не сравнение и не обмен — счётчик не растёт.</summary>
    public void Place(int index, string? desc, string? log, string memoryNote)
    {
        if (!_active) return;
        AddStep(ArrayStepKind.Place, desc!, log, "note",
            memory: [], note: memoryNote, highlights: [_work[index].Id], snap: true);
    }

    /// <summary>Пояснение без перестановок (например «минимум уже стоит на своём месте»,
    /// или заголовок слияния пары серий с подсветкой рабочего куска).</summary>
    public void Note(string desc, string log, string memoryNote, bool clearBuffer = false,
        int[]? rangeIds = null)
    {
        AddStep(ArrayStepKind.Note, desc, log, "note",
            memory: [], note: memoryNote, buffer: clearBuffer ? [] : null, rangeIds: rangeIds);
    }

    /// <summary>Числа встали на финальные места и больше не будут двигаться.</summary>
    public void MarkPlaced(params int[] indexes)
    {
        foreach (int i in indexes)
            _placed.Add(_work[i].Id);
        _placedDirty = _active;
    }

    /// <summary>Финиш: всё отсортировано.</summary>
    public void Done(string summary)
    {
        AddStep(ArrayStepKind.Done, $"🎉 {summary}", "✔ Готово", "done",
            memory: [], note: "Память свободна — сортировка закончена.");
    }

    /// <summary>Завершаем трассировку: итоговый порядок чисел (Id) и счётчики.</summary>
    public ArraySortTrace Build(int[] finalOrderIds) => new()
    {
        FinalOrder = finalOrderIds,
        TotalComparisons = _comparisons,
        TotalMoves = _moves,
        TotalPasses = Passes,
        Steps = _steps,
    };

    // ---------------- служебные ----------------

    private void AddStep(ArrayStepKind kind, string? desc, string? log, string logKind,
        int[] memory, string note,
        int[]? highlights = null, int[]? rangeIds = null, int? markId = null,
        int[]? buffer = null, bool snap = false)
    {
        if (!_active) return; // тихий режим: счётчики уже учтены, шаги не записываем
        _steps.Add(new ArraySortStep
        {
            Index = _steps.Count,
            Kind = kind,
            Phase = _phase,
            Pass = _passNo,
            PassTitle = _passTitle,
            Desc = desc ?? string.Empty,
            LogLine = log,
            LogKind = logKind,
            ArrayIds = snap ? SnapshotArray() : null,
            Highlights = highlights,
            MarkId = markId,
            RangeIds = rangeIds,
            Placed = _placedDirty ? [.. _placed] : null,
            BufferIds = buffer,
            MemoryIds = memory,
            MemoryNote = note,
            Comparisons = _comparisons,
            Moves = _moves,
            Passes = Passes,
        });
        if (_placedDirty) _placedDirty = false;
    }

    private int[] SnapshotArray()
    {
        var ids = new int[_work.Length];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = _work[i].Id;
        return ids;
    }
}
