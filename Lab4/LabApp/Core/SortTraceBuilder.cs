using LabApp.Core.ExternalSort;

namespace LabApp.Core;

/// <summary>
/// Строит пошаговую трассировку сортировки: снимки файлов, «что в памяти»,
/// объяснения простым языком и строки журнала сравнений/перестановок.
/// Стратегии сортировки пользуются его методами вместо того, чтобы собирать шаги вручную.
/// </summary>
public sealed class SortTraceBuilder
{
    private readonly TableData _table;
    private readonly List<TapeFile> _tapes = [];
    private readonly List<SortStep> _steps = [];
    private readonly int _labelColumn;
    private int _comparisons;
    private int _moves;
    private int _passes;
    private int _lastPass;
    private string _phase = "Подготовка";
    private int _pass;
    private string? _passTitle;

    public KeyComparer Comparer { get; }

    public SortTraceBuilder(TableData table, int keyColumn, bool ascending)
    {
        _table = table;
        Comparer = new KeyComparer(table, keyColumn, ascending);
        _labelColumn = PickLabelColumn(table, keyColumn);
    }

    /// <summary>Колонка, чьё значение показываем на «карточке» записи (название/страна/слово).</summary>
    public int LabelColumn => _labelColumn;

    public string LabelOf(int rowId) =>
        KeyComparer.Truncate(_table.Rows[rowId].Cells[_labelColumn], 26);

    public string KeyOf(int rowId) => Comparer.KeyText(rowId);

    /// <summary>Регистрируем файлы-ленты: они попадут в снимки состояния.</summary>
    public void Track(params TapeFile[] tapes)
    {
        foreach (var tape in tapes)
            _tapes.Add(tape);
    }

    /// <summary>Большой этап: знакомство с задачей, финиш и т.п.</summary>
    public void Phase(string title, string desc)
    {
        _phase = title;
        AddStep(StepKind.PhaseStart, desc, $"=== {title} ===", "phase",
            memory: [], note: "Память пока пуста — ещё ничего не читали.", snap: true);
    }

    /// <summary>Начало прохода: задаем «фазу» и название, которые увидят следующие шаги.</summary>
    public void Pass(int pass, string phase, string title, string desc)
    {
        _phase = phase;
        _pass = pass;
        _passTitle = $"Проход {pass}: {title}";
        if (pass != _lastPass)
        {
            _passes++;
            _lastPass = pass;
        }
        AddStep(StepKind.PassStart, desc, $"--- {_passTitle} ---", "phase",
            memory: [], note: "Память пока пуста.", snap: true);
    }

    /// <summary>Переписали запись из одного файла в другой во время разбивки/распределения.</summary>
    public void SplitMove(TapeFile from, TapeFile to, int rowId, string detail, string memoryNote)
    {
        _moves++;
        string label = LabelOf(rowId);
        AddStep(StepKind.Split,
            desc: $"Читаем «{label}» из файла {from.Name} и переписываем в {TapeWord(to)} {to.Name}. {detail}",
            log: $"↔ Перестановка №{_moves}: «{label}»: {from.Name} → {to.Name} ({detail})",
            "move", memory: [rowId], note: memoryNote, snap: true);
    }

    /// <summary>Сравнили два значения в памяти (классическое 2-путевое слияние).</summary>
    public void Compare(int leftId, int rightId, int winnerId, int rawCmp)
    {
        _comparisons++;
        string left = LabelOf(leftId), right = LabelOf(rightId), winner = LabelOf(winnerId);
        string relation = BuildRelation(leftId, rightId, rawCmp, left, right);
        string why = rawCmp == 0
            ? $"значения одинаковые — берём «{winner}» с левой ленты, чтобы порядок остался стабильным"
            : Comparer.Ascending
                ? $"{relation}; при сортировке по возрастанию первым идёт меньшее — берём «{winner}»"
                : $"{relation}; при сортировке по убыванию первым идёт большее — берём «{winner}»";

        AddStep(StepKind.Compare,
            desc: $"Сравниваем в памяти два значения: «{left}» ({Comparer.KeyText(leftId)}) и " +
                  $"«{right}» ({Comparer.KeyText(rightId)}). Выходит: {why}.",
            log: $"⚖ Сравнение №{_comparisons}: «{left}» vs «{right}» → берём «{winner}»",
            "compare", memory: [leftId, rightId], note: "В памяти ровно 2 значения — по одному с каждой ленты.",
            snap: false);
    }

    /// <summary>Сравнили значения, которых в памяти несколько (многопутевое слияние).</summary>
    public void CompareMany(int[] ids, int winnerId)
    {
        _comparisons++;
        string list = string.Join(", ", ids.Select(i => $"«{LabelOf(i)}» ({Comparer.KeyText(i)})"));
        string dir = Comparer.Ascending ? "наименьшее" : "наибольшее";
        string ending = ids.Length == 1 ? "е" : "я";
        AddStep(StepKind.Compare,
            desc: $"Смотрим на {ids.Length} значени{ending} в памяти: {list}. " +
                  $"{char.ToUpper(dir[0]) + dir[1..]} по ключевому атрибуту — «{LabelOf(winnerId)}» " +
                  $"({Comparer.KeyText(winnerId)}): его и переписываем в файл A первым.",
            log: $"⚖ Сравнение №{_comparisons}: [{list}] → {dir} «{LabelOf(winnerId)}»",
            "compare", memory: ids, note: $"В памяти {ids.Length} значени{ending} — по одному с каждой ленты.",
            snap: false);
    }

    /// <summary>Переписали запись-результат в файл (слияние). В memory — что осталось в памяти после шага.</summary>
    public void MergeMove(TapeFile to, int rowId, string detail, IReadOnlyList<int> memory, string memoryNote)
    {
        _moves++;
        string label = LabelOf(rowId);
        AddStep(StepKind.Move,
            desc: $"Переписываем «{label}» в {TapeWord(to)} {to.Name}. {detail}",
            log: $"↔ Перестановка №{_moves}: «{label}» → {to.Name} (позиция {to.Count})",
            "move", memory: memory, note: memoryNote, snap: true);
    }

    /// <summary>Одна серия закончилась — остаток другой дописываем без сравнений.</summary>
    public void FlushMove(TapeFile from, TapeFile to, int rowId, string detail)
    {
        _moves++;
        string label = LabelOf(rowId);
        AddStep(StepKind.Flush,
            desc: $"Серия на ленте {from.Name} кончилась. Остаток переписываем в {TapeWord(to)} {to.Name} " +
                  $"без сравнений: «{label}». {detail}",
            log: $"↔ Перестановка №{_moves}: «{label}»: остаток серии {from.Name} → {to.Name}",
            "move", memory: [rowId], note: "В памяти 1 значение — остаток серии.", snap: true);
    }

    /// <summary>Финиш: всё отсортировано.</summary>
    public void Done(string summary)
    {
        AddStep(StepKind.Done,
            desc: $"🎉 {summary} Мы сделали {_passes} проход(ов), сравнений — {_comparisons}, " +
                  $"перестановок (переписываний записей) — {_moves}. " +
                  "Отсортированные записи сохраняются в результирующий файл — смотри панель «Результат».",
            log: $"✔ Готово: сравнений {_comparisons}, перестановок {_moves}, проходов {_passes}",
            "done", memory: [], note: "Память свободна — сортировка закончена.", snap: true);
    }

    /// <summary>Завершаем трассировку: итоговый порядок записей.</summary>
    public SortTrace Build(IReadOnlyList<int> finalOrder) => new()
    {
        Steps = _steps,
        FinalOrder = [.. finalOrder],
        TotalComparisons = _comparisons,
        TotalMoves = _moves,
        TotalPasses = _passes,
    };

    // ---------------- служебные ----------------

    private void AddStep(StepKind kind, string desc, string? log, string logKind,
        IReadOnlyList<int> memory, string note, bool snap)
    {
        _steps.Add(new SortStep
        {
            Index = _steps.Count,
            Kind = kind,
            Phase = _phase,
            Pass = _pass,
            PassTitle = _passTitle,
            Desc = desc,
            LogLine = log,
            LogKind = logKind,
            MemoryIds = [.. memory],
            MemoryNote = note,
            Snap = snap ? SnapshotTapes() : null,
            Comparisons = _comparisons,
            Moves = _moves,
            Passes = _passes,
        });
    }

    private List<TapeSnapshot> SnapshotTapes() => _tapes
        .Select(t => new TapeSnapshot(t.Name, t.Role, t.Items.ToArray(), t.Cursor, t.RunGroups))
        .ToList();

    private string TapeWord(TapeFile tape) =>
        tape.Role == "source" ? "файл" : "ленту";

    /// <summary>Текст сравнения: для чисел — «68.2 < 146», для текста — «раньше/позже по алфавиту».</summary>
    private string BuildRelation(int a, int b, int rawCmp, string labelA, string labelB)
    {
        if (!Comparer.IsNumeric)
        {
            return rawCmp < 0 ? $"«{labelA}» стоит раньше «{labelB}» по алфавиту"
                 : rawCmp > 0 ? $"«{labelA}» стоит позже «{labelB}» по алфавиту"
                 : "значения одинаковые";
        }
        double? va = Comparer.Number(a);
        double? vb = Comparer.Number(b);
        string op = rawCmp < 0 ? "<" : rawCmp > 0 ? ">" : "=";
        return $"{Fmt(va)} {op} {Fmt(vb)}";
    }

    private string Fmt(double? value) => value is null ? "пусто" : Comparer.Format(value.Value);

    /// <summary>
    /// Выбираем колонку-«подпись» записи: если ключ сам является названием/страной/словом — берём его,
    /// иначе ищем ближайшую колонку с названием, иначе — первую колонку.
    /// </summary>
    internal static int PickLabelColumn(TableData table, int keyColumn)
    {
        const string pattern = "(назван|стран|имя|слов|name|word|товар|город)";
        var options = System.Text.RegularExpressions.RegexOptions.IgnoreCase;

        if (System.Text.RegularExpressions.Regex.IsMatch(table.Columns[keyColumn].Name, pattern, options))
            return keyColumn;

        for (int i = 0; i < table.ColumnCount; i++)
        {
            if (i != keyColumn && System.Text.RegularExpressions.Regex.IsMatch(table.Columns[i].Name, pattern, options))
                return i;
        }
        return 0;
    }
}
