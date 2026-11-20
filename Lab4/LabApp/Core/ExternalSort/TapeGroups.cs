namespace LabApp.Core.ExternalSort;

/// <summary>Служебные функции группировки записей ленты на серии (для визуализации).</summary>
public static class TapeGroups
{
    /// <summary>Режет содержимое ленты на серии фиксированной длины (последняя может быть неполной).</summary>
    public static int[][] Chunk(IReadOnlyList<int> items, int size)
    {
        size = Math.Max(size, 1);
        var groups = new List<int[]>();
        for (int pos = 0; pos < items.Count; pos += size)
        {
            int take = Math.Min(size, items.Count - pos);
            groups.Add(items.Skip(pos).Take(take).ToArray());
        }
        return groups.ToArray();
    }

    /// <summary>Группирует по известным длинам серий; последняя группа может быть неполной (текущая серия).</summary>
    public static int[][] ByLengths(IReadOnlyList<int> items, IReadOnlyList<int> lens)
    {
        var groups = new List<int[]>();
        int pos = 0;
        foreach (int len in lens)
        {
            if (pos >= items.Count)
                break;
            int take = Math.Min(len, items.Count - pos);
            groups.Add(items.Skip(pos).Take(take).ToArray());
            pos += take;
        }
        if (pos < items.Count)
            groups.Add(items.Skip(pos).ToArray());
        return groups.ToArray();
    }

    /// <summary>Длины уже готовых серий + текущая (растущая) серия.</summary>
    public static IReadOnlyList<int> WithCurrent(List<int> completed, int currentLen)
    {
        var lens = new List<int>(completed) { currentLen };
        return lens;
    }
}
