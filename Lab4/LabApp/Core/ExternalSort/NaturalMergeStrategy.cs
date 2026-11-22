namespace LabApp.Core.ExternalSort;

/// <summary>
/// Естественное слияние: сначала находим в файле куски, которые уже упорядочены
/// («естественные» серии), и сливаем их, ничего не разрезая зря. В памяти — 2 значения.
/// </summary>
public sealed class NaturalMergeStrategy : IExternalSortStrategy
{
    public string Id => "natural";
    public string Title => "Естественное слияние";
    public int MemorySlots => 2;
    public string ShortInfo =>
        "Ищем в файле уже упорядоченные куски (серии) и попарно сливаем их. Чем больше порядка в данных, тем меньше работы. В памяти 2 значения.";

    public SortTrace Execute(TableData table, int keyColumn, bool ascending)
    {
        var builder = new SortTraceBuilder(table, keyColumn, ascending);
        var cmp = builder.Comparer;
        int n = table.RowCount;
        string keyName = table.Columns[keyColumn].Name;

        var fileA = new TapeFile("A", "source");
        for (int i = 0; i < n; i++)
            fileA.Append(i);
        var fileB = new TapeFile("B", "tape");
        var fileC = new TapeFile("C", "tape");
        builder.Track(fileA, fileB, fileC);

        builder.Phase("Знакомство",
            $"Файл A — {n} записей. Сортируем по атрибуту «{keyName}» ({(ascending ? "по возрастанию" : "по убыванию")}) " +
            "методом естественного слияния: сначала находим в файле куски, которые уже стоят по порядку, " +
            "раскладываем их на две ленты и сливаем попарно, пока не останется один кусок.");

        if (n <= 1)
        {
            builder.Done(n == 0 ? "Файл пустой — сортировать нечего." : "В файле одна запись — она уже «отсортирована».");
            return builder.Build(fileA.Items);
        }

        // Серия — это кусок, внутри которого записи уже идут «в правильном» порядке.
        bool InOrder(int a, int b) =>
            ascending ? cmp.CompareRaw(a, b) <= 0 : cmp.CompareRaw(a, b) >= 0;

        var runs = DetectRuns(fileA.Items, InOrder);
        if (runs.Count <= 1)
        {
            builder.Done($"В файле всего одна серия — он уже упорядочен по атрибуту «{keyName}», проходы не нужны.");
            return builder.Build(fileA.Items);
        }

        int pass = 0;
        while (true)
        {
            pass++;
            string lengths = string.Join(", ", runs.Select(r => r.Count));

            // ---- Разбивка: готовые серии поочерёдно на B и C ----
            fileA.RunGroups = null;
            fileB.Clear();
            fileC.Clear();
            builder.Pass(pass, "Разбивка",
                $"раскладываем готовые серии из A на ленты B и C (серий: {runs.Count})",
                $"Смотрим на файл A и находим в нём куски, которые уже упорядочены. Их {runs.Count}, длины: {lengths}. " +
                "Кладём их поочерёдно: первая серия — в ленту B, вторая — в ленту C, и так далее.");

            fileA.ResetRead();
            TapeFile target = fileB;
            var bAssigned = new List<int>();
            var cAssigned = new List<int>();
            for (int r = 0; r < runs.Count; r++)
            {
                var assigned = target == fileB ? bAssigned : cAssigned;
                for (int k = 0; k < runs[r].Count; k++)
                {
                    int id = fileA.ReadNext();
                    target.Append(id);
                    target.RunGroups = TapeGroups.ByLengths(target.Items, assigned);
                    string detail = k == 0
                        ? SplitStartDetail(builder, runs, r, k, target, ascending)
                        : $"Серия №{r + 1} → лента {target.Name}, шаг {k + 1} из {runs[r].Count}. " +
                          $"Серия продолжается: новая запись — {builder.WithKey(id)}, " +
                          $"последняя запись серии — {builder.WithKey(runs[r][k - 1])}. " +
                          $"{builder.RelationText(id, runs[r][k - 1])} — порядок " +
                          $"{(ascending ? "по возрастанию" : "по убыванию")} не нарушается, " +
                          $"поэтому «{builder.LabelOf(id)}» дописываем в конец серии №{r + 1} на ленту {target.Name}.";
                    builder.SplitMove(fileA, target, id, detail,
                        "В памяти 1 значение — запись, которую сейчас переписываем.");
                }
                assigned.Add(runs[r].Count);
                target.RunGroups = TapeGroups.ByLengths(target.Items, assigned);
                target = target == fileB ? fileC : fileB;
            }

            // ---- Слияние: пары серий обратно в A ----
            builder.Pass(pass, "Слияние",
                "сливаем пары серий из B и C обратно в файл A",
                "Берём по одной серии с каждой ленты и сливаем их в одну более длинную. " +
                "В памяти ровно 2 значения: по одному с каждой ленты.");

            var bLens = runs.Where((_, i) => i % 2 == 0).Select(r => r.Count).ToList();
            var cLens = runs.Where((_, i) => i % 2 == 1).Select(r => r.Count).ToList();
            fileB.RunGroups = TapeGroups.ByLengths(fileB.Items, bLens);
            fileC.RunGroups = TapeGroups.ByLengths(fileC.Items, cLens);
            fileA.Clear();
            fileB.ResetRead();
            fileC.ResetRead();

            int pairs = Math.Max(bLens.Count, cLens.Count);
            var completed = new List<int>();
            for (int pair = 0; pair < pairs; pair++)
            {
                int bLen = pair < bLens.Count ? bLens[pair] : 0;
                int cLen = pair < cLens.Count ? cLens[pair] : 0;
                RunMerge.MergePair(builder, fileA, fileB, fileC, bLen, cLen, ascending, completed);
                completed.Add(bLen + cLen);
            }

            runs = DetectRuns(fileA.Items, InOrder);
            if (runs.Count <= 1)
            {
                builder.Done($"После слияния получилась одна серия: все {n} записей файла A стоят по порядку по атрибуту «{keyName}».");
                return builder.Build(fileA.Items);
            }
        }
    }

    /// <summary>Объяснение для первой записи серии: почему серия началась именно здесь —
    /// что сравнивалось с последней записью предыдущей серии и почему порядок нарушился.</summary>
    private static string SplitStartDetail(SortTraceBuilder builder, List<List<int>> runs, int r, int k,
        TapeFile target, bool ascending)
    {
        string head = $"Серия №{r + 1} (длина {runs[r].Count}) → лента {target.Name}, шаг {k + 1} из {runs[r].Count}.";
        if (r == 0)
            return $"{head} Это самая первая запись файла A — сравнивать её не с чем, она открывает серию №1.";
        int prevLast = runs[r - 1][^1];
        return $"{head} Серия №{r} закончилась на записи {builder.WithKey(prevLast)}, " +
               $"а новая запись — {builder.WithKey(runs[r][0])}: {builder.RelationText(prevLast, runs[r][0])}. " +
               $"При сортировке {(ascending ? "по возрастанию" : "по убыванию")} так продолжаться не может — " +
               $"порядок нарушился, поэтому «{builder.LabelOf(runs[r][0])}» начинает новую серию №{r + 1}.";
    }

    /// <summary>Разбивает последовательность записей на максимальные упорядоченные серии.</summary>
    private static List<List<int>> DetectRuns(IReadOnlyList<int> items, Func<int, int, bool> inOrder)
    {
        var runs = new List<List<int>>();
        var current = new List<int>();
        for (int i = 0; i < items.Count; i++)
        {
            if (current.Count > 0 && !inOrder(current[^1], items[i]))
            {
                runs.Add(current);
                current = new List<int>();
            }
            current.Add(items[i]);
        }
        if (current.Count > 0)
            runs.Add(current);
        return runs;
    }
}
