namespace LabApp.Core.WordSort;

/// <summary>
/// Улучшенная быстрая сортировка Хоара (QuickSort). База — классическая схема «опорное слово,
/// всё что раньше — влево, всё что позже — вправо». Улучшения:
/// 1) опорное выбирается как медиана из трёх (первое, среднее и последнее слово куска) —
///    неудачных разбиений на почти отсортированных текстах становится меньше;
/// 2) кусочки из InsertionThreshold слов и меньше досортировываются простыми вставками —
///    на маленьких кусках это быстрее рекурсии;
/// 3) рекурсия уходит только в меньший кусок, больший разбирается в цикле — глубина стека O(log n).
/// Сравнение лексикографическое — по кодам символов (string.CompareOrdinal).
/// Вызовы builder всегда безусловные (счётчики нужны и в тихом режиме), строки описаний
/// строятся только в режиме трассировки — тернарные операторы не дают их собирать при замерах.
/// </summary>
public sealed class QuickSortImproved : IWordSortAlgorithm
{
    /// <summary>Куски такого размера и меньше досортировываются вставками.</summary>
    public const int InsertionThreshold = 12;

    public string Id => "quick-improved";
    public string Title => "QuickSort (улучшенный)";
    public string Badge => "O(n·log n) в среднем";
    public string PassesLabel => "кусков";
    public int MemorySlots => 3;
    public string ShortInfo =>
        "Выбираем опорное слово (среднее из трёх) и раскладываем остальные: «раньше опорного» — влево, «позже» — вправо. " +
        "Маленькие кусочки досортировываем вставками. В памяти: опорное слово + сравниваемое + запомненное.";

    public void Sort(WordEntry[] a, bool descending, WordSortTraceBuilder b)
    {
        int n = a.Length;
        b.Phase("Знакомство",
            $"Перед нами {n} слов. Быстрая сортировка работает так: выбираем опорное слово и раскладываем остальные, " +
            "а опорные слова встают на своё окончательное место. Улучшения в этой версии: " +
            "1) опорное — среднее по алфавиту из трёх слов (первого, среднего и последнего в куске); " +
            "2) разбиение на три зоны — «раньше», «равные опорному» и «позже»: равные слова сразу на местах, " +
            "поэтому текст с частыми повторами не деградирует; " +
            $"3) кусочки до {InsertionThreshold} слов досортировываем простыми вставками; " +
            "4) в рекурсию уходим только в меньший кусок — большой разбираем в цикле, поэтому стек не переполняется. " +
            $"Порядок: {(descending ? "по убыванию (от «z» к «a»)" : "по возрастанию (от «a» к «z»)")}.");;

        SortRange(a, 0, n - 1, descending, b);
    }

    private void SortRange(WordEntry[] a, int lo, int hi, bool desc, WordSortTraceBuilder b)
    {
        while (lo < hi)
        {
            int size = hi - lo + 1;
            if (size <= InsertionThreshold)
            {
                b.PassNext("Вставки", $"кусочек [{lo}..{hi}] — сортируем вставками",
                    b.Active
                        ? $"Кусочек из {size} слов — маленький, поэтому досортируем его простыми вставками: " +
                          "берём слова по одному и вставляем каждое на его место среди уже разобранных."
                        : null,
                    RangeIds(a, lo, hi), "В памяти держим одно «взятое» слово.");
                InsertionSort(a, lo, hi, desc, b);
                return;
            }

            b.PassNext("QuickSort", $"разбираем кусок [{lo}..{hi}] из {size} слов",
                b.Active
                    ? $"Разбираем кусок из {size} слов. Сначала выберем опорное слово — медиану из трёх, " +
                      "потом разложим кусок на три зоны: «раньше опорного», «равные опорному» и «позже опорного»."
                    : null,
                RangeIds(a, lo, hi), "Память пока пуста — сейчас выберем опорное слово.");

            MedianOfThree(a, lo, hi, desc, b);
            (int lt, int gt) = Partition3Way(a, lo, hi, desc, b);
            b.MarkPlaced(Enumerable.Range(lt, gt - lt + 1).ToArray()); // зона «равных» — сразу на местах

            // В рекурсию — меньший кусок, больший продолжаем разбирать в этом же цикле:
            // глубина стека не превысит log n.
            if (lt - lo < hi - gt)
            {
                SortRange(a, lo, lt - 1, desc, b);
                lo = gt + 1;
            }
            else
            {
                SortRange(a, gt + 1, hi, desc, b);
                hi = lt - 1;
            }
        }

        if (lo == hi)
            b.MarkPlaced(lo); // кусок из одного слова — оно уже на своём месте
    }

    /// <summary>Тремя сравнениями находим медиану из a[lo], a[mid], a[hi] и ставим её в a[hi] — она станет опорной.</summary>
    private void MedianOfThree(WordEntry[] a, int lo, int hi, bool desc, WordSortTraceBuilder b)
    {
        int mid = lo + (hi - lo) / 2;

        b.PassNext("Опорное слово", "выбираем медиану из трёх",
            b.Active
                ? "Смотрим на три слова куска — первое, среднее и последнее — и сравнениями выбираем " +
                  "среднее по алфавиту: оно и станет опорным. Так опорное почти никогда не оказывается самым «крайним», " +
                  "и сортировка не деградирует даже на почти отсортированном тексте."
                : null,
            RangeIds(a, lo, hi), "В памяти три слова-кандидата.");

        // Шаг 1: меньшее из a[lo], a[mid] ставим в a[lo].
        bool firstLess = Less(a[mid].Text, a[lo].Text, desc);
        b.Compare(mid, lo,
            b.Active
                ? $"Кандидаты — «{a[lo].Text}» (позиция {lo}) и «{a[mid].Text}» (позиция {mid}). " +
                  $"{Relation(a[mid].Text, a[lo].Text)}: меньшее из них отправляем в начало куска."
                : null,
            b.Active
                ? $"⚖ Сравнение №{b.Comparisons + 1}: медиана трёх — «{a[mid].Text}» vs «{a[lo].Text}»"
                : null,
            "В памяти два слова-кандидата.");
        if (firstLess)
        {
            (a[mid], a[lo]) = (a[lo], a[mid]);
            b.Swap(mid, lo,
                b.Active
                    ? $"Меняем местами: теперь на позиции {lo} стоит «{a[lo].Text}» — меньшее из первой пары."
                    : null,
                b.Active
                    ? $"↔ Перестановка №{b.Moves + 1}: «{a[mid].Text}» ⇄ «{a[lo].Text}» (выбор опорного)"
                    : null,
                "В памяти два слова-кандидата.");
        }

        // Шаг 2: минимум всех трёх собираем в a[lo].
        bool secondLess = Less(a[hi].Text, a[lo].Text, desc);
        b.Compare(hi, lo,
            b.Active
                ? $"Сравниваем «{a[hi].Text}» (последнее) с «{a[lo].Text}»: {Relation(a[hi].Text, a[lo].Text)}. " +
                  "Так в начале куска окажется минимум всех трёх кандидатов."
                : null,
            b.Active
                ? $"⚖ Сравнение №{b.Comparisons + 1}: медиана трёх — «{a[hi].Text}» vs «{a[lo].Text}»"
                : null,
            "В памяти два слова-кандидата.");
        if (secondLess)
        {
            (a[hi], a[lo]) = (a[lo], a[hi]);
            b.Swap(hi, lo,
                b.Active
                    ? $"Меняем местами: минимум «{a[lo].Text}» теперь в начале куска."
                    : null,
                b.Active
                    ? $"↔ Перестановка №{b.Moves + 1}: «{a[hi].Text}» ⇄ «{a[lo].Text}» (выбор опорного)"
                    : null,
                "В памяти два слова-кандидата.");
        }

        // Шаг 3: из двух оставшихся среднее по алфавиту ставим в a[hi] — это опорное.
        bool thirdLess = Less(a[mid].Text, a[hi].Text, desc);
        b.Compare(mid, hi,
            b.Active
                ? $"Остались «{a[mid].Text}» и «{a[hi].Text}»: {Relation(a[mid].Text, a[hi].Text)}. " +
                  "Среднее из них ставим в конец куска — это опорное слово."
                : null,
            b.Active
                ? $"⚖ Сравнение №{b.Comparisons + 1}: медиана трёх — «{a[mid].Text}» vs «{a[hi].Text}»"
                : null,
            "В памяти два слова-кандидата.");
        if (thirdLess)
        {
            (a[mid], a[hi]) = (a[hi], a[mid]);
            b.Swap(mid, hi,
                b.Active
                    ? $"Меняем местами: опорным становится «{a[hi].Text}» — среднее по алфавиту из трёх."
                    : null,
                b.Active
                    ? $"↔ Перестановка №{b.Moves + 1}: «{a[mid].Text}» ⇄ «{a[hi].Text}» (выбор опорного)"
                    : null,
                "В памяти два слова-кандидата.");
        }
    }

    /// <summary>
    /// Трёхпутевое разбиение (Дейкстра, «голландский флаг») — усовершенствование для текстов с повторами:
    /// кусок делится на три зоны — «раньше опорного» [lo..lt-1], «равно опорному» [lt..gt] и «позже опорного» [gt+1..hi].
    /// Равные опорному слова сразу встают на свои места, поэтому текст с частыми повторами
    /// (типичный случай для слов текста) не деградирует до O(n²).
    /// Возвращает границы зоны «равных»: lt и gt.
    /// </summary>
    private (int Lt, int Gt) Partition3Way(WordEntry[] a, int lo, int hi, bool desc, WordSortTraceBuilder b)
    {
        int pivotIndex = hi;
        string pivot = a[hi].Text;
        string leftZone = desc ? "позже опорного" : "раньше опорного";
        string rightZone = desc ? "раньше опорного" : "позже опорного";

        b.PassNext("Разбиение", $"опорное — «{pivot}»",
            b.Active
                ? $"Идём по куску слева направо и каждое слово сравниваем с опорным «{pivot}»: " +
                  $"{leftZone} — меняем в левую зону, равные опорному — остаются в середине и сразу встают на свои места, " +
                  $"{rightZone} — меняем в правую зону. В конце кусок разбит на три зоны, средняя уже отсортирована."
                : null,
            RangeIds(a, lo, hi), $"В памяти опорное слово «{pivot}» и текущее сравниваемое.");

        int lt = lo;     // граница левой зоны: слова [lo..lt-1] — {leftZone}
        int i = lo;      // текущее слово
        int gt = hi;     // граница правой зоны: слова [gt+1..hi] — {rightZone}; [lt..i-1] — равные опорному

        while (i <= gt)
        {
            // Одно сравнение с опорным: -1 «раньше», 0 «равно», +1 «позже» (с учётом направления).
            int c = string.CompareOrdinal(a[i].Text, pivot) * (desc ? -1 : 1);
            bool toLeft = c < 0, toRight = c > 0;
            b.Compare(i, pivotIndex,
                b.Active
                    ? $"Сравнили «{a[i].Text}» (позиция {i}) с опорным «{pivot}»: {Relation(a[i].Text, pivot)}. " +
                      (toLeft ? $"«{a[i].Text}» — {leftZone}: меняем в левую зону (позиция {lt})."
                      : toRight ? $"«{a[i].Text}» — {rightZone}: меняем в правую зону (позиция {gt})."
                      : $"«{a[i].Text}» равен опорному — остаётся в середине, он уже на своём месте!")
                    : null,
                b.Active
                    ? $"⚖ Сравнение №{b.Comparisons + 1}: «{a[i].Text}» vs опорное «{pivot}» → " +
                      (toLeft ? leftZone : toRight ? rightZone : "равно — на месте")
                    : null,
                $"В памяти опорное «{pivot}» и текущее «{a[i].Text}».", pivotIndex);

            if (toLeft)
            {
                if (lt != i)
                {
                    (a[lt], a[i]) = (a[i], a[lt]);
                    b.Swap(lt, i,
                        b.Active
                            ? $"Обменяли позиции {lt} и {i}: «{a[lt].Text}» ушло в левую зону, «{a[i].Text}» — на проверку."
                            : null,
                        b.Active
                            ? $"↔ Перестановка №{b.Moves + 1}: «{a[lt].Text}» ⇄ «{a[i].Text}» (левая зона)"
                            : null,
                        "Меняем два слова местами — в памяти оба.");
                }
                lt++;
                i++;
            }
            else if (toRight)
            {
                if (gt != i)
                {
                    (a[gt], a[i]) = (a[i], a[gt]);
                    b.Swap(gt, i,
                        b.Active
                            ? $"Обменяли позиции {gt} и {i}: «{a[gt].Text}» ушло в правую зону, на позицию {i} пришло новое слово — проверим его тоже."
                            : null,
                        b.Active
                            ? $"↔ Перестановка №{b.Moves + 1}: «{a[gt].Text}» ⇄ «{a[i].Text}» (правая зона)"
                            : null,
                        "Меняем два слова местами — в памяти оба.");
                }
                gt--; // i не двигается: на позицию i пришло ещё не проверенное слово
            }
            else
            {
                i++; // равное опорному — в зоне «равных», на своём месте
            }
        }

        if (b.Active)
            b.Note(
                $"Кусок разобран на три зоны: [{lo}..{lt - 1}] — {leftZone} ({lt - lo} {PluralCount(lt - lo)}), " +
                $"[{lt}..{gt}] — равные опорному «{pivot}» ({gt - lt + 1} {PluralCount(gt - lt + 1)}), " +
                $"[{gt + 1}..{hi}] — {rightZone} ({hi - gt} {PluralCount(hi - gt)}). " +
                "Слова зоны «равных» на своих местах — дальше разбираем только края.",
                $"✓ Разбиение [{lo}..{hi}] на 3 зоны вокруг «{pivot}»",
                "Слова зоны «равных» помечены как стоящие на местах.");
        return (lt, gt);
    }

    /// <summary>Сортировка простыми вставками на малом куске: каждое слово вставляем на место среди уже разобранных.</summary>
    private void InsertionSort(WordEntry[] a, int lo, int hi, bool desc, WordSortTraceBuilder b)
    {
        for (int x = lo + 1; x <= hi; x++)
        {
            WordEntry key = a[x];
            int y = x - 1;
            while (y >= lo)
            {
                bool less = Less(key.Text, a[y].Text, desc);
                b.Compare(y, x,
                    b.Active
                        ? $"Взяли слово «{key.Text}» и сравниваем с «{a[y].Text}» (позиция {y}): {Relation(key.Text, a[y].Text)}. " +
                          (less
                              ? $"«{key.Text}» должно стоять раньше — сдвигаем «{a[y].Text}» на позицию вправо и смотрим дальше."
                              : $"«{key.Text}» должно стоять позже — нашли его место: позиция {y + 1}.")
                        : null,
                    b.Active
                        ? $"⚖ Сравнение №{b.Comparisons + 1}: вставки — «{key.Text}» vs «{a[y].Text}» → {(less ? "сдвигаем" : "на месте")}"
                        : null,
                    "В памяти «взятое» слово и слово, с которым сравниваем.");
                if (!less) break;
                a[y + 1] = a[y];
                b.Shift(y, y + 1,
                    b.Active
                        ? $"Сдвинули «{a[y + 1].Text}» с позиции {y} на {y + 1} — освободили место для «{key.Text}»."
                        : null,
                    b.Active
                        ? $"↔ Перестановка №{b.Moves + 1}: «{a[y + 1].Text}»: позиция {y} → {y + 1} (вставки)"
                        : null,
                    "В памяти «взятое» слово — оно сейчас «в руке», не в массиве.");
                y--;
            }
            a[y + 1] = key; // слово опускается на своё место — это не обмен, счётчик не растёт
        }
        b.MarkPlaced(Enumerable.Range(lo, hi - lo + 1).ToArray());
    }

    // ---------------- служебные ----------------

    /// <summary>Лексикографическое «раньше» с учётом направления сортировки.</summary>
    private static bool Less(string x, string y, bool desc) =>
        desc ? string.CompareOrdinal(x, y) > 0 : string.CompareOrdinal(x, y) < 0;

    /// <summary>Словоформы для чисел 1, 2, 5 (слово/слова/слов).</summary>
    private static string PluralCount(int n)
    {
        int mod10 = n % 10, mod100 = n % 100;
        if (mod10 == 1 && mod100 != 11) return "слово";
        if (mod10 is >= 2 and <= 4 && (mod100 < 12 || mod100 > 14)) return "слова";
        return "слов";
    }

    /// <summary>Человеческое описание результата сравнения двух слов.</summary>
    private static string Relation(string x, string y)
    {
        int c = string.CompareOrdinal(x, y);
        return c < 0 ? $"«{x}» стоит раньше «{y}» по алфавиту"
             : c > 0 ? $"«{x}» стоит позже «{y}» по алфавиту"
             : "слова одинаковые";
    }

    private static int[] RangeIds(WordEntry[] a, int lo, int hi)
    {
        var ids = new int[hi - lo + 1];
        for (int i = lo; i <= hi; i++) ids[i - lo] = a[i].Id;
        return ids;
    }
}
