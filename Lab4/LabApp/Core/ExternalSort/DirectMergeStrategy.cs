namespace LabApp.Core.ExternalSort;

/// <summary>
/// Прямое слияние — классический двухпутевый алгоритм внешней сортировки.
/// Каждый проход: 1) файл A разрезаем на ленты B и C сериями длины p;
/// 2) сливаем B и C обратно в A, получая серии длины 2p.
/// Во время слияния в памяти ровно 2 значения — по одному с каждой ленты.
/// </summary>
public sealed class DirectMergeStrategy : IExternalSortStrategy
{
    public string Id => "direct";
    public string Title => "Прямое слияние";
    public int MemorySlots => 2;
    public string ShortInfo =>
        "Режем файл на две ленты и сливаем обратно, каждый раз удваивая длину упорядоченных серий. В памяти ровно 2 значения.";

    public SortTrace Execute(TableData table, int keyColumn, bool ascending)
    {
        var builder = new SortTraceBuilder(table, keyColumn, ascending);
        int n = table.RowCount;
        string keyName = table.Columns[keyColumn].Name;

        var fileA = new TapeFile("A", "source");
        for (int i = 0; i < n; i++)
            fileA.Append(i);
        var fileB = new TapeFile("B", "tape");
        var fileC = new TapeFile("C", "tape");
        builder.Track(fileA, fileB, fileC);

        builder.Phase("Знакомство",
            $"Перед нами файл A — в нём {n} записей. Считаем, что каждая отдельная запись — это упорядоченная «серия» длины 1. " +
            $"Сортируем по атрибуту «{keyName}» ({(ascending ? "по возрастанию" : "по убыванию")}) методом прямого слияния: " +
            "разрезаем файл на две ленты, сливаем обратно — и повторяем, пока весь файл не станет одной большой серией.");

        if (n <= 1)
        {
            builder.Done(n == 0 ? "Файл пустой — сортировать нечего." : "В файле одна запись — она уже «отсортирована».");
            return builder.Build(fileA.Items);
        }

        int p = 1;  // длина серий на лентах перед слиянием
        int pass = 0;

        while (true)
        {
            pass++;

            // ---- Этап 1: разбивка A → B, C ----
            fileA.RunGroups = null;
            fileB.Clear();
            fileC.Clear();
            fileA.ResetRead();
            builder.Pass(pass, "Разбивка",
                $"разрезаем файл A на ленты B и C (серии по {p})",
                $"Берём из файла A записи по {p} штук подряд (это серии) и раскладываем их поочерёдно: " +
                $"первая серия — в ленту B, вторая — в ленту C, и так до конца файла. " +
                "Пока в памяти держим только одну текущую запись.");

            TapeFile target = fileB;
            int inRun = 0;
            while (fileA.HasNext)
            {
                int id = fileA.ReadNext();
                if (inRun == p)
                {
                    inRun = 0;
                    target = target == fileB ? fileC : fileB;
                }
                target.Append(id);
                target.RunGroups = TapeGroups.Chunk(target.Items, p);
                inRun++;
                int runNo = (target.Count + p - 1) / p;
                builder.SplitMove(fileA, target, id,
                    $"серия №{runNo} на ленте {target.Name}, шаг {inRun} из {p}",
                    "В памяти 1 значение — запись, которую сейчас переписываем.");
            }

            // Всё уместилось в одну серию — файл уже упорядочен.
            if (fileC.Count == 0)
            {
                builder.Done("Все записи уложились в одну серию — файл A упорядочен.");
                return builder.Build(fileA.Items);
            }

            // ---- Этап 2: слияние B + C → A ----
            builder.Pass(pass, "Слияние",
                $"сливаем ленты B и C обратно в файл A (серии растут до {2 * p})",
                "Теперь берём по одной серии с каждой ленты и сливаем их в одну. " +
                "В памяти ровно 2 значения: текущая запись из ленты B и текущая из ленты C. " +
                "Сравниваем их и переписываем в файл A ту, что должна идти раньше.");

            fileB.RunGroups = TapeGroups.Chunk(fileB.Items, p);
            fileC.RunGroups = TapeGroups.Chunk(fileC.Items, p);
            fileA.Clear();
            fileB.ResetRead();
            fileC.ResetRead();
            var completed = new List<int>();
            while (fileB.HasNext || fileC.HasNext)
            {
                int bLen = Math.Min(p, fileB.Count - fileB.Cursor);
                int cLen = Math.Min(p, fileC.Count - fileC.Cursor);
                RunMerge.MergePair(builder, fileA, fileB, fileC, bLen, cLen, ascending, completed);
                completed.Add(bLen + cLen);
            }

            // После слияния серии длиной 2p покрывают весь файл — конец.
            if (2 * p >= n)
            {
                builder.Done($"Файл A снова один: все {n} записей стоят по порядку по атрибуту «{keyName}».");
                return builder.Build(fileA.Items);
            }
            p *= 2;
        }
    }
}
