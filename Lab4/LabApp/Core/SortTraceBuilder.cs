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
        AddStep(StepKind.Split,
            desc: $"Читаем {WithKey(rowId)} из файла {from.Name} и переписываем в {TapeWord(to)} {to.Name}. {detail}",
            log: $"↔ Перестановка №{_moves}: {WithKey(rowId)}: {from.Name} → {to.Name} ({detail})",
            "move", memory: [rowId], note: memoryNote, snap: true);
    }

    /// <summary>Сравнили два значения в памяти (классическое 2-путевое слияние).</summary>
    public void Compare(int leftId, int rightId, int winnerId, int rawCmp)
    {
        _comparisons++;
        string relation = BuildRelation(leftId, rightId, rawCmp);
        string rule = rawCmp == 0
            ? "при равенстве первой берём запись с левой ленты, чтобы порядок остался стабильным"
            : Comparer.IsNumeric
                ? Comparer.Ascending
                    ? "при сортировке по возрастанию первым идёт меньшее значение"
                    : "при сортировке по убыванию первым идёт большее значение"
                : Comparer.Ascending
                    ? "при сортировке по возрастанию первым идёт более раннее значение"
                    : "при сортировке по убыванию первым идёт более позднее значение";

        AddStep(StepKind.Compare,
            desc: $"Сравнили в памяти два значения — по одному с каждой ленты: {WithKey(leftId)} и {WithKey(rightId)}. " +
                  $"Выходит: {relation} — {rule}, поэтому берём {WithKey(winnerId)}.",
            log: $"⚖ Сравнение №{_comparisons}: {WithKey(leftId)} vs {WithKey(rightId)} → {relation} — {rule} → берём {WithKey(winnerId)}",
            "compare", memory: [leftId, rightId], note: "В памяти ровно 2 значения — по одному с каждой ленты.",
            snap: false);
    }

    /// <summary>Сравнили значения, которых в памяти несколько (многопутевое слияние).</summary>
    public void CompareMany(int[] ids, int winnerId)
    {
        _comparisons++;
        string list = string.Join(", ", ids.Select(WithKey));
        string dir = Comparer.Ascending ? "наименьшее" : "наибольшее";
        string ending = ids.Length == 1 ? "е" : "я";
        string reason = SelectionReason(winnerId, ids);
        AddStep(StepKind.Compare,
            desc: $"Смотрим на {ids.Length} значени{ending} в памяти — по одному с каждой ленты: {list}. " +
                  $"Каждый шаг выбираем {dir} по ключевому атрибуту. Выходит: {reason}. " +
                  $"Переписываем в файл A {WithKey(winnerId)}.",
            log: $"⚖ Сравнение №{_comparisons}: [{list}] → {reason} → берём {WithKey(winnerId)}",
            "compare", memory: ids, note: $"В памяти {ids.Length} значени{ending} — по одному с каждой ленты.",
            snap: false);
    }

    /// <summary>Переписали запись-результат в файл (слияние). В memory — что осталось в памяти после шага.
    /// because — объяснение, почему выбрана именно эта запись (итог прошлого сравнения);
    /// fromName — лента, с которой пришла запись (попадает в строку журнала).</summary>
    public void MergeMove(TapeFile to, int rowId, string detail, IReadOnlyList<int> memory, string memoryNote,
        string? because = null, string? fromName = null)
    {
        _moves++;
        string lead = because is null ? "" : because + " ";
        string log = fromName is null
            ? $"↔ Перестановка №{_moves}: {WithKey(rowId)} → {to.Name} (позиция {to.Count})"
            : $"↔ Перестановка №{_moves}: {WithKey(rowId)}: {fromName} → {to.Name} (позиция {to.Count})";
        AddStep(StepKind.Move,
            desc: $"{lead}Переписываем {WithKey(rowId)} в {TapeWord(to)} {to.Name}. {detail}",
            log: log,
            "move", memory: memory, note: memoryNote, snap: true);
    }

    /// <summary>Одна серия закончилась — остаток другой дописываем без сравнений.</summary>
    public void FlushMove(TapeFile from, TapeFile to, int rowId, string detail)
    {
        _moves++;
        AddStep(StepKind.Flush,
            desc: $"Серия на ленте {from.Name} кончилась — сравнивать больше не с чем. " +
                  $"В памяти осталось одно значение {WithKey(rowId)}: дописываем его и весь остаток серии " +
                  $"в {TapeWord(to)} {to.Name} без сравнений. {detail}",
            log: $"↔ Перестановка №{_moves}: {WithKey(rowId)}: остаток серии {from.Name} → {to.Name}",
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

    /// <summary>Текст сравнения — всегда по значениям ключевого атрибута, а не по названиям записей:
    /// для чисел — «68.2 < 146», для текста — «„Канберра“ стоит раньше „Буэнос-Айрес“ по алфавиту».</summary>
    private string BuildRelation(int a, int b, int rawCmp)
    {
        if (!Comparer.IsNumeric)
        {
            string ka = Comparer.KeyText(a), kb = Comparer.KeyText(b);
            return rawCmp < 0 ? $"«{ka}» стоит раньше «{kb}» по алфавиту"
                 : rawCmp > 0 ? $"«{ka}» стоит позже «{kb}» по алфавиту"
                 : "значения одинаковые";
        }
        double? va = Comparer.Number(a);
        double? vb = Comparer.Number(b);
        string op = rawCmp < 0 ? "<" : rawCmp > 0 ? ">" : "=";
        return $"{Fmt(va)} {op} {Fmt(vb)}";
    }

    private string Fmt(double? value) => value is null ? "пусто" : Comparer.Format(value.Value);

    /// <summary>Подпись записи со значением ключа: «Вода» (18.015); если подпись и есть ключ — без скобок.
    /// Публичная: стратегии вставляют подпись с ключом в свои пояснения.</summary>
    public string WithKey(int rowId)
    {
        string label = LabelOf(rowId);
        string key = Comparer.KeyText(rowId);
        return key == label || key == "пусто" ? $"«{label}»" : $"«{label}» ({key})";
    }

    /// <summary>Компактный итог сравнения двух записей: «44.01 &gt; 18.015» для чисел,
    /// «„Канберра“ стоит позже „Буэнос-Айрес“ по алфавиту» для текста — по значениям ключа.</summary>
    public string RelationText(int aId, int bId)
    {
        int raw = Comparer.CompareRaw(aId, bId);
        return BuildRelation(aId, bId, raw);
    }

    /// <summary>Почему из значений памяти выбрана именно эта запись: её связь с каждым из остальных,
    /// у каждого сравниваемого значения — подпись записи.</summary>
    public string SelectionReason(int winnerId, IReadOnlyList<int> ids)
    {
        var others = ids.Where(i => i != winnerId).ToList();
        if (others.Count == 0)
            return "сравнивать не с кем — в памяти осталась одна запись";
        if (!Comparer.IsNumeric)
        {
            string kw = Comparer.KeyText(winnerId);
            var keys = others.Select(o => $"«{Comparer.KeyText(o)}»").ToList();
            string listStr = keys.Count == 1 ? keys[0]
                : string.Join(", ", keys.Take(keys.Count - 1)) + " и " + keys[^1];
            return Comparer.Ascending
                ? $"«{kw}» стоит раньше {listStr} по алфавиту"
                : $"«{kw}» стоит позже {listStr} по алфавиту";
        }
        var parts = others.Select(o =>
            $"{BuildRelation(winnerId, o, Comparer.CompareRaw(winnerId, o))} («{LabelOf(o)}»)");
        string best = Fmt(Comparer.Number(winnerId));
        return string.Join(", ", parts) + $" — {best} " + (Comparer.Ascending ? "наименьшее" : "наибольшее");
    }

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
