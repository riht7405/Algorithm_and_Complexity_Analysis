namespace LabApp.Core.ExternalSort;

/// <summary>
/// Общая часть прямого и естественного слияния: сливаем одну серию с ленты B
/// и одну серию с ленты C в файл A. В памяти ровно 2 значения.
/// </summary>
public static class RunMerge
{
    /// <param name="a">файл-результат</param>
    /// <param name="b">левая лента</param>
    /// <param name="c">правая лента</param>
    /// <param name="bLen">сколько записей читать из текущей серии ленты B</param>
    /// <param name="cLen">сколько записей читать из текущей серии ленты C</param>
    /// <param name="ascending">направление сортировки</param>
    /// <param name="completedLens">длины уже готовых серий результата (для группировки на панели A)</param>
    public static void MergePair(SortTraceBuilder builder, TapeFile a, TapeFile b, TapeFile c,
        int bLen, int cLen, bool ascending, List<int> completedLens)
    {
        var cmp = builder.Comparer;
        int startCount = a.Count;

        // Берём по одному значению с каждой ленты — в памяти ровно 2 значения.
        int? x = bLen > 0 ? b.ReadNext() : null;
        int? y = cLen > 0 ? c.ReadNext() : null;
        int bx = 1, cy = 1;

        while (x is not null || y is not null)
        {
            if (x is int xv && y is int yv)
            {
                int raw = cmp.CompareRaw(xv, yv);
                bool takeLeft = ascending ? raw <= 0 : raw >= 0; // при равенстве — с левой ленты
                int winner = takeLeft ? xv : yv;
                builder.Compare(xv, yv, winner, raw);

                a.Append(winner);
                a.RunGroups = TapeGroups.ByLengths(a.Items, TapeGroups.WithCurrent(completedLens, a.Count - startCount));

                string fromName = takeLeft ? "B" : "C";
                int? next = takeLeft
                    ? (bx < bLen ? b.ReadNext() : null)
                    : (cy < cLen ? c.ReadNext() : null);
                if (takeLeft) bx++; else cy++;

                // В памяти остался проигравший + только что прочитанная следующая запись.
                var memory = new List<int> { takeLeft ? yv : xv };
                if (next is int nid)
                    memory.Add(nid);

                string refill = next is int n2
                    ? $"На освободившееся место из ленты {fromName} приходит «{builder.LabelOf(n2)}»."
                    : $"Серия на ленте {fromName} закончилась.";
                builder.MergeMove(a, winner, $"Серия-результат в файле A растёт. {refill}",
                    memory,
                    memory.Count == 2
                        ? "В памяти снова 2 значения."
                        : "В памяти 1 значение — вторая серия уже закончилась.");

                if (takeLeft) x = next; else y = next;
            }
            else if (x is int rest1)
            {
                // Серия на C кончилась: остаток B дописываем без сравнений.
                a.Append(rest1);
                a.RunGroups = TapeGroups.ByLengths(a.Items, TapeGroups.WithCurrent(completedLens, a.Count - startCount));
                builder.FlushMove(b, a, rest1, "Хвост серии переписан целиком.");
                x = bx < bLen ? b.ReadNext() : null;
                bx++;
            }
            else if (y is int rest2)
            {
                // Серия на B кончилась: остаток C дописываем без сравнений.
                a.Append(rest2);
                a.RunGroups = TapeGroups.ByLengths(a.Items, TapeGroups.WithCurrent(completedLens, a.Count - startCount));
                builder.FlushMove(c, a, rest2, "Хвост серии переписан целиком.");
                y = cy < cLen ? c.ReadNext() : null;
                cy++;
            }
        }
    }
}
