namespace LabApp.Core.ExternalSort;

/// <summary>
/// Многопутевое слияние на 4 лентах: раздаём записи по кругу на ленты T1…T4,
/// затем сливаем все ленты разом — в памяти до 4 значений, каждый шаг берём
/// наименьшее (или наибольшее, если сортируем по убыванию). Серии растут в 4 раза
/// быстрее, чем при прямом слиянии.
/// </summary>
public sealed class MultiwayMergeStrategy : IExternalSortStrategy
{
    public const int TapeCount = 4;

    public string Id => "multiway";
    public string Title => "Многопутевое слияние (4 ленты)";
    public int MemorySlots => TapeCount;
    public string ShortInfo =>
        "Раздаём записи по кругу на 4 ленты, потом сливаем все ленты разом: в памяти до 4 значений, каждый раз берём наименьшее. Серии растут в 4 раза быстрее.";

    public SortTrace Execute(TableData table, int keyColumn, bool ascending)
    {
        var builder = new SortTraceBuilder(table, keyColumn, ascending);
        int n = table.RowCount;
        string keyName = table.Columns[keyColumn].Name;

        var fileA = new TapeFile("A", "source");
        for (int i = 0; i < n; i++)
            fileA.Append(i);
        var tapes = new TapeFile[TapeCount];
        for (int i = 0; i < TapeCount; i++)
            tapes[i] = new TapeFile($"T{i + 1}", "tape");
        builder.Track([fileA, .. tapes]);

        builder.Phase("Знакомство",
            $"Файл A — {n} записей. Сортируем по атрибуту «{keyName}» ({(ascending ? "по возрастанию" : "по убыванию")}) " +
            $"многопутевым слиянием на {TapeCount} лентах: раздаём записи по кругу на ленты T1…T{TapeCount}, " +
            $"потом сливаем их все разом — в памяти лежит до {TapeCount} значений, и каждый шаг мы берём " +
            $"{(ascending ? "наименьшее" : "наибольшее")} из них.");

        if (n <= 1)
        {
            builder.Done(n == 0 ? "Файл пустой — сортировать нечего." : "В файле одна запись — она уже «отсортирована».");
            return builder.Build(fileA.Items);
        }

        // ---- Распределение: по одной записи по кругу на ленты ----
        fileA.RunGroups = null;
        builder.Pass(1, "Распределение",
            $"раздаём записи из A по кругу на {TapeCount} лент",
            $"Идём по файлу A и раздаём записи по кругу: первая — в T1, вторая — в T2, и так далее, " +
            $"{TapeCount + 1}-я — снова в T1. После раздачи на каждой ленте серии длиной 1.");

        fileA.ResetRead();
        int idx = 0;
        while (fileA.HasNext)
        {
            int id = fileA.ReadNext();
            TapeFile t = tapes[idx % TapeCount];
            t.Append(id);
            string detail = idx == 0
                ? $"Первая запись файла A открывает раздачу — идёт на ленту {t.Name}. Серии пока длиной 1: каждая запись сама по себе."
                : $"Раздаём по кругу: предыдущая запись ушла на ленту {tapes[(idx - 1) % TapeCount].Name}, " +
                  $"эта идёт на ленту {t.Name}.";
            builder.SplitMove(fileA, t, id, detail,
                "В памяти 1 значение — запись, которую сейчас раздаём.");
            idx++;
        }

        int runLen = 1; // длина серий на лентах перед слиянием
        int pass = 1;

        while (true)
        {
            pass++;

            // ---- Слияние: все ленты → файл A ----
            builder.Pass(pass, "Слияние",
                $"сливаем {TapeCount} лент в файл A (серии растут до {runLen * TapeCount})",
                $"Читаем с каждой ленты по одному значению — в памяти до {TapeCount} значений. " +
                $"Каждый шаг сравниваем их и переписываем в файл A {(ascending ? "наименьшее" : "наибольшее")}.");

            foreach (var t in tapes)
                t.RunGroups = TapeGroups.Chunk(t.Items, runLen);
            fileA.Clear();
            foreach (var t in tapes)
                t.ResetRead();

            int produced = MergePass(builder, fileA, tapes, runLen, ascending);
            int newLen = runLen * TapeCount;

            if (produced <= 1)
            {
                builder.Done($"Все серии слились в одну: файл A упорядочен по атрибуту «{keyName}», " +
                             $"ленты {string.Join(", ", tapes.Select(t => t.Name))} опустели.");
                return builder.Build(fileA.Items);
            }

            // ---- Перераспределение: серии из A обратно по лентам ----
            builder.Pass(pass, "Распределение",
                $"раскладываем серии из A обратно по {TapeCount} лентам (серии по {newLen})",
                $"В файле A пока {produced} серий — повторяем цикл. Раздаём готовые серии по кругу на ленты, " +
                $"по {newLen} записей в серии.");

            fileA.RunGroups = null;
            fileA.ResetRead();
            foreach (var t in tapes)
                t.Clear();
            int tapeIdx = 0;
            while (fileA.HasNext)
            {
                int take = Math.Min(newLen, fileA.Count - fileA.Cursor);
                TapeFile t = tapes[tapeIdx % TapeCount];
                int runNo = t.Count / Math.Max(newLen, 1) + 1;
                for (int k = 0; k < take; k++)
                {
                    int id = fileA.ReadNext();
                    t.Append(id);
                    t.RunGroups = TapeGroups.Chunk(t.Items, newLen);
                    builder.SplitMove(fileA, t, id,
                        $"Серия №{runNo} (длина {take}) → лента {t.Name}, шаг {k + 1} из {take}",
                        "В памяти 1 значение — запись, которую сейчас переписываем.");
                }
                tapeIdx++;
            }

            runLen = newLen;
        }
    }

    /// <summary>Один проход слияния всех лент в файл A. Возвращает количество получившихся серий.</summary>
    private static int MergePass(SortTraceBuilder builder, TapeFile fileA, TapeFile[] tapes,
        int runLen, bool ascending)
    {
        var cmp = builder.Comparer;

        // Сколько записей осталось прочитать с каждой ленты в её текущей серии.
        int[] remaining = tapes.Select(t => Math.Min(runLen, t.Count - t.Cursor)).ToArray();

        // Читаем первое значение с каждой непустой ленты — это наша «память».
        var memory = new List<int>();
        var memoryTape = new List<int>();
        for (int i = 0; i < tapes.Length; i++)
        {
            if (remaining[i] > 0)
            {
                memory.Add(tapes[i].ReadNext());
                memoryTape.Add(i);
                remaining[i]--;
            }
        }

        var completed = new List<int>();
        int inRun = 0;
        int produced = 0;

        while (memory.Count > 0)
        {
            // Ищем лучшую запись: наименьшую (по возрастанию) или наибольшую (по убыванию).
            // При равенстве остаётся запись с более левой ленты — порядок стабильный.
            int best = 0;
            for (int i = 1; i < memory.Count; i++)
            {
                int raw = cmp.CompareRaw(memory[i], memory[best]);
                if (ascending ? raw < 0 : raw > 0)
                    best = i;
            }

            builder.CompareMany(memory.ToArray(), memory[best]);

            // Дописали в A — читаем с этой ленты следующую запись её серии.
            fileA.Append(memory[best]);
            inRun++;
            fileA.RunGroups = TapeGroups.ByLengths(fileA.Items, TapeGroups.WithCurrent(completed, inRun));

            int tapeIdx = memoryTape[best];
            int? next = null;
            if (remaining[tapeIdx] > 0)
            {
                next = tapes[tapeIdx].ReadNext();
                remaining[tapeIdx]--;
            }

            var after = new List<int>();
            for (int i = 0; i < memory.Count; i++)
            {
                if (i != best)
                    after.Add(memory[i]);
            }
            if (next is int nid)
                after.Add(nid);

            string refill = next is int
                ? $"На его место с ленты {tapes[tapeIdx].Name} приходит {builder.WithKey(next.Value)}."
                : $"Лента {tapes[tapeIdx].Name} в этой серии закончилась — новое значение на его место не приходит.";
            string because = $"Победила на прошлом шаге сравнения: {builder.SelectionReason(memory[best], memory)}.";
            builder.MergeMove(fileA, memory[best],
                $"Серия-результат в файле A растёт. {refill}",
                after,
                $"В памяти {after.Count} значени{(after.Count == 1 ? "е" : "я")} — по одному с каждой ленты, где серия ещё не закончилась.",
                because, tapes[tapeIdx].Name);

            if (next is not null)
            {
                memory[best] = next.Value;
            }
            else
            {
                memory.RemoveAt(best);
                memoryTape.RemoveAt(best);
                if (memory.Count == 0)
                {
                    // Текущие серии со всех лент выбраны — в A получилась одна готовая серия.
                    produced++;
                    completed.Add(inRun);
                    inRun = 0;

                    // Начинаем следующие серии: читаем по одному значению с лент, где ещё есть данные.
                    for (int i = 0; i < tapes.Length; i++)
                    {
                        int left = tapes[i].Count - tapes[i].Cursor;
                        if (left > 0)
                        {
                            memory.Add(tapes[i].ReadNext());
                            memoryTape.Add(i);
                            remaining[i] = Math.Min(runLen, left) - 1;
                        }
                    }
                }
            }
        }

        return produced;
    }
}
