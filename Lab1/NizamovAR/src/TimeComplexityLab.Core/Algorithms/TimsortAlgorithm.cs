using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class TimsortAlgorithm : IAlgorithm
{
    private const int Run = 32;

    public string Id => "sorting.timsort";
    public string DisplayName => "Гибридная сортировка Timsort";
    public string Category => "Сортировки";
    public string TheoreticalComplexity => "O(n log n)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public ExperimentDefaults Defaults => new(
        NMax: 5_000_000, NStep: 10_000, MMax: 0, MStep: 0,
        RunsPerPoint: 5, Hypothesis: "NLogN");

    public object GenerateSource(ExperimentPoint maxPoint, Random random)
    {
        var v = new double[maxPoint.N];
        for (int i = 0; i < v.Length; i++) v[i] = random.NextDouble() * 1000.0;
        return v;
    }

    public object Slice(object source, ExperimentPoint point)
    {
        var v = (double[])source;
        if (point.N >= v.Length) return v;

        var slice = new double[point.N];
        Array.Copy(v, slice, point.N);
        return slice;
    }

    public object Clone(object input) => ((double[])input).Clone();

    public void Execute(object input, IExecutionTracker tracker)
    {
        var v = (double[])input;
        Sort(v);
    }

    private static void Sort(double[] v)
    {
        int n = v.Length;

        for (int i = 0; i < n; i += Run)
            InsertionSort(v, i, Math.Min(i + Run - 1, n - 1));

        for (int size = Run; size < n; size = 2 * size)
            for (int left = 0; left < n; left += 2 * size)
            {
                int mid = left + size - 1;
                int right = Math.Min(left + 2 * size - 1, n - 1);
                if (mid < right) Merge(v, left, mid, right);
            }
    }

    private static void InsertionSort(double[] v, int left, int right)
    {
        for (int i = left + 1; i <= right; i++)
        {
            double temp = v[i];
            int j = i - 1;
            while (j >= left && v[j] > temp)
            {
                v[j + 1] = v[j];
                j--;
            }
            v[j + 1] = temp;
        }
    }

    private static void Merge(double[] v, int l, int m, int r)
    {
        int len1 = m - l + 1;
        int len2 = r - m;
        var left = new double[len1];
        var right = new double[len2];
        Array.Copy(v, l, left, 0, len1);
        Array.Copy(v, m + 1, right, 0, len2);

        int i = 0, j = 0, k = l;
        while (i < len1 && j < len2)
        {
            if (left[i] <= right[j]) v[k++] = left[i++];
            else v[k++] = right[j++];
        }
        while (i < len1) v[k++] = left[i++];
        while (j < len2) v[k++] = right[j++];
    }
}