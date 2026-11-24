namespace LabApp.Core.WordSort;

/// <summary>
/// ABC-сортировка — поразрядная сортировка по буквам («корзины по алфавиту»).
/// Первый уровень: раскладываем все слова по корзинам по первой букве (A — в корзину A, B — в корзину B…).
/// Затем каждую корзину с более чем одним словом разбираем так же по второй букве, и так далее.
/// Когда буквы кончились, корзины собирают в порядке алфавита — слова отсортированы,
/// а одинаковые слова оказываются рядом, что удобно для подсчёта вхождений.
/// Слова, которые целиком «уложились» в общий префикс (букв больше нет), идут первыми
/// при сортировке по возрастанию и последними — по убыванию.
/// </summary>
public sealed class AbcSort : IWordSortAlgorithm
{
    public string Id => "abc";
    public string Title => "ABC-сортировка";
    public string Badge => "O(n·k), k — буквы";
    public string PassesLabel => "уровней букв";
    public int MemorySlots => 2;
    public string ShortInfo =>
        "Раскладываем слова по корзинам по первой букве, потом каждую корзину — по второй, и так далее. " +
        "Собираем корзины в порядке алфавита. В памяти: текущее слово и буква, которую ищем.";

    public void Sort(WordEntry[] a, bool descending, WordSortTraceBuilder b)
    {
        int n = a.Length;
        b.Phase("Знакомство",
            $"Перед нами {n} слов. ABC-сортировка раскладывает слова по корзинам-буквам: на первом уровне — " +
            "по первой букве, на втором уровне каждая корзина разбирается по второй букве, и так далее, " +
            "пока у слов не кончатся буквы. Потом корзины собирают по порядку алфавита — и массив отсортирован. " +
            "Одинаковые слова всегда попадают в одну корзину, поэтому в отсортированном массиве идут рядом. " +
            $"Порядок: {(descending ? "по убыванию (от «z» к «a»)" : "по возрастанию (от «a» к «z»)")}.");

        SortSegment(a, 0, n, 0, descending, b);
    }

    private void SortSegment(WordEntry[] a, int lo, int hi, int depth, bool desc, WordSortTraceBuilder b)
    {
        int size = hi - lo;
        if (size <= 1)
        {
            if (size == 1) b.MarkPlaced(lo);
            return;
        }

        // Все слова куска одинаковые — они уже «по порядку», раскладывать их не нужно.
        bool allSame = true;
        for (int i = lo + 1; i < hi; i++)
        {
            if (string.CompareOrdinal(a[i].Text, a[lo].Text) != 0) { allSame = false; break; }
        }
        if (allSame)
        {
            b.MarkPlaced(Enumerable.Range(lo, size).ToArray());
            if (b.Active)
            {
                b.Note(
                    $"Слова с позиции {lo} по {hi - 1} все одинаковые — «{a[lo].Text}». Одинаковые слова уже стоят рядом, " +
                    "двигать их не нужно: помечаем их как стоящие на своих местах.",
                    $"✓ Кусок [{lo}..{hi - 1}]: все слова «{a[lo].Text}» — уже по порядку",
                    "Память свободна — сравнивать внутри куска нечего.");
            }
            return;
        }

        b.PassLevel(depth + 1, "ABC",
            $"раскладываем {size} слов по букве №{depth + 1}",
            b.Active
                ? $"Берём кусок из {size} слов и раскладываем по корзинам: смотрим у каждого слова букву №{depth + 1} " +
                  $"и кладём слово в корзину с этой буквой. Слова, у которых букв уже нет (они совпали с общим началом), " +
                  $"кладём в корзину «✓» — {(desc ? "такие слова идут последними" : "такие слова идут первыми")}."
                : null,
            RangeIds(a, lo, hi), "В памяти одно текущее слово и буква, которую ищем.");

        // ---- Распределение по корзинам ----
        var buckets = new Dictionary<char, List<int>>();  // буква → позиции слов в массиве
        var letters = new List<char>();                   // порядок появления букв
        var shorts = new List<int>();                     // слова без буквы №depth+1

        for (int i = lo; i < hi; i++)
        {
            string word = a[i].Text;
            if (word.Length <= depth)
            {
                shorts.Add(i);
                b.Place(i, "✓", 0,
                    b.Active ? SnapshotBuckets(buckets, letters, shorts, a, desc) : null,
                    b.Active
                        ? $"Слово «{word}» (позиция {i}) целиком совпало с общим началом — у него нет буквы №{depth + 1}. " +
                          $"Кладём его в корзину «✓»: {(desc ? "такие слова собираются в конце" : "такие слова собираются в начале")}."
                        : null,
                    b.Active
                        ? $"↔ Перестановка №{b.Moves + 1}: «{word}» → корзина ✓ (без сравнений букв)"
                        : null,
                    "В памяти одно слово — его положили в корзину «✓».");
                continue;
            }

            char letter = word[depth];
            // Ищем корзину с этой буквой: сравниваем букву слова с буквами существующих корзин.
            int scanComparisons = 0;
            bool found = false;
            foreach (char existing in letters)
            {
                scanComparisons++;
                if (existing == letter) { found = true; break; }
            }
            if (!found)
            {
                letters.Add(letter);
                buckets[letter] = [];
            }
            buckets[letter].Add(i);

            b.Place(i, letter.ToString(), scanComparisons,
                b.Active ? SnapshotBuckets(buckets, letters, shorts, a, desc) : null,
                b.Active
                    ? $"Слово «{word}» (позиция {i}): буква №{depth + 1} — «{letter}». " +
                      (found
                          ? $"Корзина «{letter}» уже есть — нашлась после {scanComparisons} " + Plural(scanComparisons, "сравнения", "сравнений", "сравнений") + ". "
                          : $"Корзин с такой буквой не было — создали её (сравнений: {scanComparisons}). ") +
                      $"Кладём «{word}» в корзину «{letter}»."
                    : null,
                b.Active
                    ? $"↔ Перестановка №{b.Moves + 1}: «{word}» → корзина «{letter}» (букв сравнили: {scanComparisons})"
                    : null,
                "В памяти одно слово — его положили в свою корзину.");
        }

        // ---- Сборка корзин обратно в массив ----
        var orderedLetters = letters.ToArray();
        Array.Sort(orderedLetters); // порядок корзин при сборке: алфавитный (для убывания перевернём при обходе)
        var groups = new List<List<int>>(orderedLetters.Length + 1); // группы позиций в порядке сборки
        var groupNames = new List<string>();

        void AddGroup(List<int> items, string name)
        {
            if (items.Count > 0) { groups.Add(items); groupNames.Add(name); }
        }

        IEnumerable<char> letterOrder = desc ? orderedLetters.Reverse() : orderedLetters;
        if (!desc) AddGroup(shorts, "✓");
        foreach (char c in letterOrder)
            AddGroup(buckets[c], c.ToString());
        if (desc) AddGroup(shorts, "✓");

        b.Collect(size,
            b.Active
                ? $"Все слова разложены. Собираем корзины обратно в массив {(desc ? "от «z» к «a»" : "от «a» к «z»")}: " +
                  $"{string.Join(", ", groups.Select((g, k) => $"{groupNames[k]} ({g.Count} " + Plural(g.Count, "слово", "слова", "слов") + ")"))}. " +
                  "Теперь любое слово из более ранней корзины стоит раньше любого слова из более поздней."
                : null,
            b.Active
                ? $"↔ Перестановка №{b.Moves}: сборка корзин → массив ({string.Join(", ", groupNames)})"
                : null);

        // переписываем по-настоящему: сначала во временный список, затем обратно в массив
        var rewritten = new WordEntry[size];
        int pos = 0;
        foreach (var group in groups)
            foreach (int idx in group)
                rewritten[pos++] = a[idx];
        Array.Copy(rewritten, 0, a, lo, size);

        // ---- Рекурсивно разбираем каждую корзину по следующей букве ----
        int start = lo;
        for (int g = 0; g < groups.Count; g++)
        {
            List<int> group = groups[g];
            if (group.Count > 1 && groupNames[g] != "✓")
            {
                SortSegment(a, start, start + group.Count, depth + 1, desc, b);
            }
            else
            {
                b.MarkPlaced(Enumerable.Range(start, group.Count).ToArray());
            }
            start += group.Count;
        }
    }

    // ---------------- служебные ----------------

    /// <summary>Снимок корзин в порядке сборки: «✓» (если есть) и буквенные корзины по алфавиту.</summary>
    private static List<BucketSnapshot>? SnapshotBuckets(
        Dictionary<char, List<int>> buckets, List<char> letters, List<int> shorts, WordEntry[] a, bool desc)
    {
        if (buckets.Count == 0 && shorts.Count == 0) return null;
        var result = new List<BucketSnapshot>();
        var orderedLetters = letters.ToArray();
        Array.Sort(orderedLetters);
        if (!desc && shorts.Count > 0)
            result.Add(new BucketSnapshot("✓", shorts.Select(i => a[i].Id).ToArray()));
        foreach (char c in (desc ? orderedLetters.Reverse() : orderedLetters))
            result.Add(new BucketSnapshot(c.ToString(), buckets[c].Select(i => a[i].Id).ToArray()));
        if (desc && shorts.Count > 0)
            result.Add(new BucketSnapshot("✓", shorts.Select(i => a[i].Id).ToArray()));
        return result;
    }

    private static int[] RangeIds(WordEntry[] a, int lo, int hi)
    {
        var ids = new int[hi - lo];
        for (int i = lo; i < hi; i++) ids[i - lo] = a[i].Id;
        return ids;
    }

    /// <summary>Словоформы для чисел 1, 2, 5 (сравнения/слова).</summary>
    private static string Plural(int n, string one, string few, string many)
    {
        int mod10 = n % 10, mod100 = n % 100;
        if (mod10 == 1 && mod100 != 11) return one;
        if (mod10 is >= 2 and <= 4 && (mod100 < 12 || mod100 > 14)) return few;
        return many;
    }
}
