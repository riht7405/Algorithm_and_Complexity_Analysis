namespace LabApp.Core.ArraySort;

/// <summary>
/// Сортировка выбором (SelectSort) — квадратичный алгоритм. На каждом проходе ищем минимум
/// в неотсортированной части [i..n-1] и ставим его в начало этой части (позиция i):
/// после прохода позиция i — окончательное место своего числа. Обменов ровно не больше n-1,
/// поэтому алгоритм хорош, когда перестановки дорогие; сравнений всегда n(n-1)/2 —
/// даже на уже отсортированном массиве.
/// Вызовы builder всегда безусловные (счётчики нужны и в тихом режиме), строки описаний
/// строятся только в режиме трассировки — тернарные операторы не дают их собирать при замерах.
/// </summary>
public sealed class SelectSort : IArraySortAlgorithm
{
    public string Id => "select";
    public string Title => "SelectSort (выбором)";
    public string Group => ArraySortGroups.Quadratic;
    public string Badge => "O(n²)";
    public string PassesLabel => "проходов";
    public int MemorySlots => 2;
    public string ShortInfo =>
        "Ищем самое маленькое число в неотсортированной части и ставим его в начало — так растёт отсортированная часть. " +
        "В памяти: проверяемое число и текущий минимум.";

    public void Sort(ArrayEntry[] a, bool descending, ArraySortTraceBuilder b)
    {
        int n = a.Length;
        string wanted = descending ? "самое большое" : "самое маленькое";
        string sortedSide = descending ? "с конца" : "с начала";

        b.Phase("Знакомство",
            $"Перед нами массив из {n} чисел. Сортировка выбором работает так: на каждом проходе ищем " +
            $"{wanted} число в неотсортированной части и ставим его на границу — отсортированная часть " +
            $"растёт {sortedSide} на одно число за проход. Обменов здесь мало (не больше {Math.Max(n - 1, 0)}), " +
            $"а вот сравнений всегда n·(n-1)/2 — минимум приходится искать до конца, даже если всё уже по порядку. " +
            $"Порядок: {(descending ? "по убыванию" : "по возрастанию")}.");

        for (int i = 0; i < n - 1; i++)
        {
            b.PassNext("Выбор минимума", $"ищем {wanted} в позициях [{i}..{n - 1}]",
                b.Active
                    ? $"Проход {i + 1}: неотсортированная часть — позиции [{i}..{n - 1}]. " +
                      $"Просмотрим её всю и найдём {wanted} число, а потом поставим его на позицию {i}."
                    : null,
                RangeIds(a, i, n - 1), "Память пока пуста — сейчас выберем первый кандидат в минимум.");

            int min = i;
            for (int j = i + 1; j < n; j++)
            {
                bool less = Less(a[j].Value, a[min].Value, descending);
                b.Compare(j, min,
                    b.Active
                        ? $"Сравнили «{a[j].Value}» (позиция {j}) с текущим {wantedNominal(descending)} " +
                          $"«{a[min].Value}» (позиция {min}): {Relation(a[j].Value, a[min].Value, descending)}. " +
                          (less ? $"«{a[j].Value}» лучше — запоминаем его как новый кандидат."
                                : $"Оставляем «{a[min].Value}» — он пока лучший.")
                        : null,
                    b.Active
                        ? $"⚖ Сравнение №{b.Comparisons + 1}: «{a[j].Value}» vs «{a[min].Value}»" +
                          (less ? " → новый кандидат" : "")
                        : null,
                    "В памяти проверяемое число и текущий «лучший» кандидат.", min);
                if (less) min = j;
            }

            if (min != i)
            {
                (a[i], a[min]) = (a[min], a[i]);
                b.Swap(i, min,
                    b.Active
                        ? $"Проход закончен: {wantedNominal(descending)} — «{a[i].Value}», ставим его на позицию {i}. " +
                          $"Число с позиции {i} («{a[min].Value}») переезжает на место «{a[i].Value}»."
                        : null,
                    b.Active
                        ? $"↔ Перестановка №{b.Moves + 1}: «{a[i].Value}» ⇄ «{a[min].Value}» (позиции {i} и {min})"
                        : null,
                    "В памяти оба меняющихся числа.");
            }
            else if (b.Active)
            {
                b.Note(
                    $"Проход закончен: {wantedNominal(descending)} — «{a[i].Value}» — уже стоит на позиции {i}. " +
                    "Обмен не нужен.",
                    $"✓ Позиция {i}: «{a[i].Value}» уже на месте — без обмена",
                    "Обмен не понадобился — память свободна.");
            }
            b.MarkPlaced(i); // позиция i — окончательное место своего числа
        }

        if (n > 0) b.MarkPlaced(n - 1); // последнее число ставится само, прохода для него нет
        b.Done(
            n == 0
                ? "Массив пустой — сортировать нечего."
                : $"Готово: все числа стоят по {(descending ? "убыванию" : "возрастанию")}. " +
                  $"Сравнений — {b.Comparisons}, перестановок — {b.Moves}, проходов — {b.Passes}.");
    }

    // ---------------- служебные ----------------

    /// <summary>«Раньше» с учётом направления сортировки.</summary>
    private static bool Less(int x, int y, bool desc) => desc ? x > y : x < y;

    private static string wantedNominal(bool desc) => desc ? "максимум" : "минимум";

    /// <summary>Человеческое описание результата сравнения двух чисел.</summary>
    private static string Relation(int x, int y, bool desc)
    {
        if (x == y) return "числа одинаковые";
        bool xBetter = desc ? x > y : x < y;
        return xBetter
            ? $"«{x}» подходит больше (нужно {(desc ? "наибольшее" : "наименьшее")})"
            : $"«{x}» не лучше текущего";
    }

    private static int[] RangeIds(ArrayEntry[] a, int lo, int hi)
    {
        var ids = new int[hi - lo + 1];
        for (int i = lo; i <= hi; i++) ids[i - lo] = a[i].Id;
        return ids;
    }
}
