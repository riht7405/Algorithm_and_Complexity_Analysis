namespace LabApp.Core.WordSort;

/// <summary>
/// Строит пошаговую трассировку сортировки слов: снимки массива и корзин, «что в памяти»,
/// объяснения простым языком и строки журнала сравнений/перестановок. Счётчики живут здесь —
/// один источник правды и для визуализации, и для быстрых замеров времени.
/// В тихом режиме (active: false) шаги не собираются и строки не строятся — остаются только счётчики,
/// поэтому замеры времени не тратят время на строки.
/// </summary>
public sealed class WordSortTraceBuilder
{
    /// <summary>Рабочий массив алгоритма: builder делает его снимки, алгоритм его переставляет.</summary>
    private readonly WordEntry[] _work;
    private readonly bool _active;
    private readonly List<WordSortStep> _steps = [];
    private readonly HashSet<int> _placed = [];     // слова, стоящие на финальных местах
    private bool _placedDirty = true;               // первый шаг должен включить список placed

    private long _comparisons;
    private long _moves;
    private int _autoPass;                          // авто-нумерация проходов (QuickSort — куски)
    private int _maxLevel;                          // максимальный уровень букв (ABC)
    private string _phase = "Подготовка";
    private int _passNo;
    private string? _passTitle;

    public WordSortTraceBuilder(WordEntry[] work, bool active)
    {
        _work = work;
        _active = active;
    }

    /// <summary>false — тихий режим: шаги не записываются, описания можно не строить.</summary>
    public bool Active => _active;
    public long Comparisons => _comparisons;
    public long Moves => _moves;
    public int Passes => Math.Max(_autoPass, _maxLevel);

    /// <summary>Большой этап: знакомство с задачей, подсчёт, финиш.</summary>
    public void Phase(string title, string desc)
    {
        _phase = title;
        AddStep(WordStepKind.PhaseStart, desc, $"=== {title} ===", "phase",
            memory: [], note: "Память пока пуста — ещё ничего не сравнивали.");
    }

    /// <summary>Начало прохода с авто-номером: каждый разбор куска в QuickSort.
    /// desc может быть null в тихом режиме (замер времени) — шаг всё равно не пишется.</summary>
    public void PassNext(string phase, string title, string? desc, int[]? rangeIds, string memoryNote)
    {
        _phase = phase;
        _autoPass++;
        _passNo = _autoPass;
        _passTitle = $"Проход {_autoPass}: {title}";
        AddStep(WordStepKind.PassStart, desc, $"--- {_passTitle} ---", "phase",
            memory: [], note: memoryNote, rangeIds: rangeIds);
    }

    /// <summary>Начало прохода с явным уровнем (ABC): счётчик проходов = самый глубокий уровень букв.</summary>
    public void PassLevel(int level, string phase, string title, string? desc, int[]? rangeIds, string memoryNote)
    {
        _phase = phase;
        if (level > _maxLevel) _maxLevel = level;
        _passNo = level;
        _passTitle = $"Уровень {level}: {title}";
        AddStep(WordStepKind.PassStart, desc, $"--- {_passTitle} ---", "phase",
            memory: [], note: memoryNote, rangeIds: rangeIds);
    }

    /// <summary>Сравнили два слова (стоят в массиве на позициях i и j).</summary>
    public void Compare(int i, int j, string? desc, string? log, string memoryNote, int? pivotIndex = null)
    {
        _comparisons++;
        if (!_active) return;
        AddStep(WordStepKind.Compare,
            desc!, log,
            "compare",
            memory: [_work[i].Id, _work[j].Id],
            note: memoryNote,
            highlights: [_work[i].Id, _work[j].Id],
            pivotId: pivotIndex is null ? null : _work[pivotIndex.Value].Id);
    }

    /// <summary>Массив уже переставлен алгоритмом — фиксируем обмен позиций i и j снимком.</summary>
    public void Swap(int i, int j, string? desc, string? log, string memoryNote)
    {
        _moves++;
        if (!_active) return;
        AddStep(WordStepKind.Swap, desc!, log, "move",
            memory: [_work[i].Id, _work[j].Id], note: memoryNote,
            highlights: [_work[i].Id, _work[j].Id], snap: true);
    }

    /// <summary>Сдвиг слова из позиции from в позицию to (вставки на малых кусках). Массив уже сдвинут.</summary>
    public void Shift(int from, int to, string? desc, string? log, string memoryNote)
    {
        _moves++;
        if (!_active) return;
        AddStep(WordStepKind.Shift, desc!, log, "move",
            memory: [_work[to].Id], note: memoryNote,
            highlights: [_work[to].Id, _work[from].Id], snap: true);
    }

    /// <summary>Слово положили в корзину (ABC). lookupComparisons — сколько букв пришлось сравнить,
    /// чтобы найти корзину; buckets — снимок всех корзин текущего куска.</summary>
    public void Place(int index, string bucketLetter, int lookupComparisons,
        List<BucketSnapshot>? buckets, string? desc, string? log, string memoryNote)
    {
        _comparisons += lookupComparisons;
        _moves++;
        if (!_active) return;
        AddStep(WordStepKind.Place, desc!, log, "move",
            memory: [_work[index].Id], note: memoryNote,
            highlights: [_work[index].Id], buckets: buckets);
    }

    /// <summary>Корзины собрали обратно в массив (ABC). Массив уже переписан — фиксируем снимком.</summary>
    public void Collect(int size, string? desc, string? log)
    {
        _moves += size;
        if (!_active) return;
        AddStep(WordStepKind.Collect, desc!, log, "move",
            memory: [], note: "Слова лежат в массиве, память свободна.", snap: true);
    }

    /// <summary>Пояснение без перестановок (например «все слова куска одинаковые — они уже по порядку»).</summary>
    public void Note(string desc, string log, string memoryNote)
    {
        AddStep(WordStepKind.Note, desc, log, "note",
            memory: [], note: memoryNote);
    }

    /// <summary>Слова встали на финальные места и больше не будут двигаться.</summary>
    public void MarkPlaced(params int[] indexes)
    {
        foreach (int i in indexes)
            _placed.Add(_work[i].Id);
        _placedDirty = _active;
    }

    /// <summary>Шаг подсчёта: очередное слово отсортированного массива и сколько раз оно идёт подряд.</summary>
    public void Count(int wordId, int count, string desc, string log)
    {
        AddStep(WordStepKind.Count, desc, log, "count", memory: [wordId],
            note: "Идём по отсортированному массиву: одинаковые слова стоят рядом.");
    }

    public void CountSummary(string desc)
    {
        AddStep(WordStepKind.Count, desc, "--- Подсчёт: остальные слова ---", "count",
            memory: [], note: "Полная таблица подсчёта — на панели «Подсчёт слов».");
    }

    /// <summary>
    /// Фаза подсчёта вхождений: идём по отсортированному массиву, одинаковые слова идут подряд —
    /// считаем длину каждой серии. Подробно показываем первые detailedLimit серий, остальные — одной строкой.
    /// </summary>
    public void CountingPhase(IReadOnlyList<WordCount> counts, int detailedLimit)
    {
        Phase("Подсчёт",
            $"Сортировка закончена: одинаковые слова теперь стоят подряд. Пройдёмся по отсортированному массиву " +
            $"и для каждого слова посчитаем, сколько раз оно встречается, — всего уникальных слов: {counts.Count}.");
        for (int k = 0; k < counts.Count; k++)
        {
            var group = counts[k];
            if (k < detailedLimit)
            {
                string times = group.Count == 1 ? "1 раз" : $"{group.Count} " + Plural(group.Count, "раз", "раза", "раз");
                Count(group.WordId, group.Count,
                    $"Идём по массиву: слово «{group.Word}» повторяется {times} подряд — записываем: " +
                    $"{group.Count} " + Plural(group.Count, "вхождение", "вхождения", "вхождений") + ".",
                    $"Σ «{group.Word}» — {group.Count} " + Plural(group.Count, "вхождение", "вхождения", "вхождений"));
            }
        }
        if (counts.Count > detailedLimit)
            CountSummary($"Оставшиеся {counts.Count - detailedLimit} уникальных слов подсчитаны точно так же — " +
                "полная таблица на панели «Подсчёт слов».");
    }

    /// <summary>Финиш: всё отсортировано и подсчитано.</summary>
    public void Done(string summary)
    {
        AddStep(WordStepKind.Done, $"🎉 {summary}", "✔ Готово", "done",
            memory: [], note: "Память свободна — сортировка закончена.");
    }

    /// <summary>Завершаем трассировку: итоговый порядок слов (Id) и счётчики.</summary>
    public WordSortTrace Build(int[] finalOrderIds) => new()
    {
        FinalOrder = finalOrderIds,
        TotalComparisons = _comparisons,
        TotalMoves = _moves,
        TotalPasses = Passes,
        Steps = _steps,
    };

    // ---------------- служебные ----------------

    private void AddStep(WordStepKind kind, string? desc, string? log, string logKind,
        int[] memory, string note,
        int[]? highlights = null, int[]? rangeIds = null, int? pivotId = null,
        List<BucketSnapshot>? buckets = null, bool snap = false)
    {
        if (!_active) return; // тихий режим: счётчики уже учтены, шаги не записываем
        _steps.Add(new WordSortStep
        {
            Index = _steps.Count,
            Kind = kind,
            Phase = _phase,
            Pass = _passNo,
            PassTitle = _passTitle,
            Desc = desc,
            LogLine = log,
            LogKind = logKind,
            ArrayIds = snap ? SnapshotArray() : null,
            Highlights = highlights,
            PivotId = pivotId,
            RangeIds = rangeIds,
            Placed = _placedDirty ? [.. _placed] : null,
            MemoryIds = memory,
            MemoryNote = note,
            Buckets = buckets,
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

    /// <summary>Словоформы для чисел 1, 2, 5 (раз/раза/раз и т.п.).</summary>
    private static string Plural(int n, string one, string few, string many)
    {
        int mod10 = n % 10, mod100 = n % 100;
        if (mod10 == 1 && mod100 != 11) return one;
        if (mod10 is >= 2 and <= 4 && (mod100 < 12 || mod100 > 14)) return few;
        return many;
    }
}
